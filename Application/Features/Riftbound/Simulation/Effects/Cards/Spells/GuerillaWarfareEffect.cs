using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class GuerillaWarfareEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "guerilla-warfare";
    public override string TemplateId => "named.guerilla-warfare";

    public override bool TryAddLegalActions(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        List<RiftboundLegalAction> actions
    )
    {
        actions.Add(
            new RiftboundLegalAction(
                $"{runtime.ActionPrefix}play-{card.InstanceId}-spell",
                RiftboundActionType.PlayCard,
                player.PlayerIndex,
                $"Play spell {card.Name}"
            )
        );
        return true;
    }

    public override void OnSpellOrGearPlay(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        string actionId
    )
    {
        var hiddenCards = player.TrashZone.Cards
            .Where(x => x.Keywords.Contains("Hidden", StringComparer.OrdinalIgnoreCase))
            .OrderBy(x => x.Name, StringComparer.Ordinal)
            .ThenBy(x => x.InstanceId)
            .Take(2)
            .ToList();
        foreach (var hidden in hiddenCards)
        {
            if (!player.TrashZone.Cards.Remove(hidden))
            {
                continue;
            }

            player.HandZone.Cards.Add(hidden);
        }

        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "Resolve",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["returnedHiddenCards"] = hiddenCards.Count.ToString(),
                ["hideIgnoringCostsThisTurn"] = "true",
            }
        );
    }
}

