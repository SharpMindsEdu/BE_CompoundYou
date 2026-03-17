using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class MindsplitterEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "mindsplitter";
    public override string TemplateId => "named.mindsplitter";

    public override void OnUnitPlay(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        string actionId
    )
    {
        var opponent = session.Players.FirstOrDefault(x => x.PlayerIndex != player.PlayerIndex);
        if (opponent is null || opponent.HandZone.Cards.Count == 0)
        {
            return;
        }

        var discarded = opponent.HandZone.Cards
            .OrderByDescending(x => x.Cost.GetValueOrDefault())
            .ThenByDescending(x => x.Power.GetValueOrDefault())
            .ThenByDescending(x => x.Might.GetValueOrDefault())
            .ThenBy(x => x.Name, StringComparer.Ordinal)
            .ThenBy(x => x.InstanceId)
            .First();
        opponent.HandZone.Cards.Remove(discarded);
        opponent.TrashZone.Cards.Add(discarded);

        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "WhenPlay",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["targetOpponent"] = opponent.PlayerIndex.ToString(),
                ["discarded"] = discarded.Name,
            }
        );
    }
}

