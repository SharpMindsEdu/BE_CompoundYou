using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class AlbusFerrosEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "albus-ferros";
    public override string TemplateId => "named.albus-ferros";

    public override void OnUnitPlay(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        string actionId
    )
    {
        var spendableBuffs = ResolveSpendableBuffs(session, player.PlayerIndex)
            .Where(x => x.Unit.InstanceId != card.InstanceId)
            .ToList();
        if (spendableBuffs.Count == 0)
        {
            return;
        }

        var buffsSpent = 0;
        foreach (var buffSource in spendableBuffs)
        {
            while (buffSource.Unit.PermanentMightModifier > 0)
            {
                buffSource.Unit.PermanentMightModifier -= 1;
                buffsSpent += 1;
            }
        }

        if (buffsSpent <= 0)
        {
            return;
        }

        var channeled = 0;
        for (var i = 0; i < buffsSpent; i += 1)
        {
            if (player.RuneDeckZone.Cards.Count == 0)
            {
                break;
            }

            var rune = player.RuneDeckZone.Cards[0];
            player.RuneDeckZone.Cards.RemoveAt(0);
            rune.IsExhausted = true;
            player.BaseZone.Cards.Add(rune);
            channeled += 1;
        }

        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "WhenPlay",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["buffsSpent"] = buffsSpent.ToString(),
                ["channeledRunesExhausted"] = channeled.ToString(),
            }
        );
    }

    private static IReadOnlyCollection<(CardInstance Unit, string Location)> ResolveSpendableBuffs(
        GameSession session,
        int playerIndex
    )
    {
        var candidates = new List<(CardInstance Unit, string Location)>();
        candidates.AddRange(
            session.Players[playerIndex]
                .BaseZone.Cards.Where(x => string.Equals(x.Type, "Unit", StringComparison.OrdinalIgnoreCase))
                .Select(x => (x, "base"))
        );
        foreach (var battlefield in session.Battlefields)
        {
            candidates.AddRange(
                battlefield.Units
                    .Where(x => x.ControllerPlayerIndex == playerIndex)
                    .Select(x => (x, $"bf-{battlefield.Index}"))
            );
        }

        return candidates
            .Where(x => x.Unit.PermanentMightModifier > 0)
            .OrderByDescending(x => x.Unit.PermanentMightModifier)
            .ThenBy(x => x.Unit.Name, StringComparer.Ordinal)
            .ThenBy(x => x.Unit.InstanceId)
            .ToList();
    }
}
