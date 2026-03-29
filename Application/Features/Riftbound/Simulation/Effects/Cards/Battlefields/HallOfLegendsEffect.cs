using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class HallOfLegendsEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "hall-of-legends";
    public override string TemplateId => "named.hall-of-legends";

    public override void OnConquer(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        BattlefieldState battlefield,
        string? sourceActionId
    )
    {
        var legend = player.LegendZone.Cards.FirstOrDefault(x => x.IsExhausted);
        if (legend is null)
        {
            return;
        }

        if (!runtime.TryPayCost(session, player, energyCost: 1))
        {
            return;
        }

        legend.IsExhausted = false;
        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "WhenConquer",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["battlefield"] = battlefield.Name,
                ["readiedLegend"] = legend.Name,
                ["paidEnergy"] = "1",
            }
        );
    }
}

