using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class JaxUnrelentingEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "jax-unrelenting";
    public override string TemplateId => "named.jax-unrelenting";

    public override void OnGearAttached(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        CardInstance attachedGear,
        CardInstance targetUnit
    )
    {
        if (
            targetUnit.InstanceId != card.InstanceId
            || targetUnit.ControllerPlayerIndex != player.PlayerIndex
            || attachedGear.ControllerPlayerIndex != player.PlayerIndex
        )
        {
            return;
        }

        if (!runtime.TryPayCost(session, player, energyCost: 1))
        {
            return;
        }

        runtime.DrawCards(player, 1);
        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "WhenEquipAttached",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["target"] = card.Name,
                ["attachedGear"] = attachedGear.Name,
                ["paidEnergy"] = "1",
                ["drawn"] = "1",
            }
        );
    }
}
