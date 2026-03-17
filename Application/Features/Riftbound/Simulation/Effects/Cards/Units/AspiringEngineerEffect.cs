using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class AspiringEngineerEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "aspiring-engineer";
    public override string TemplateId => "named.aspiring-engineer";

    public override void OnUnitPlay(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        string actionId
    )
    {
        var gear = player.TrashZone.Cards
            .Where(x => string.Equals(x.Type, "Gear", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.Cost.GetValueOrDefault())
            .ThenByDescending(x => x.Power.GetValueOrDefault())
            .ThenBy(x => x.Name, StringComparer.Ordinal)
            .ThenBy(x => x.InstanceId)
            .FirstOrDefault();
        if (gear is null)
        {
            return;
        }

        player.TrashZone.Cards.Remove(gear);
        player.HandZone.Cards.Add(gear);
        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "WhenPlay",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["returnedGear"] = gear.Name,
            }
        );
    }
}

