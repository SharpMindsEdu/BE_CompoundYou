using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class DropboarderEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "dropboarder";
    public override string TemplateId => "named.dropboarder";

    public override void OnUnitPlay(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        string actionId
    )
    {
        var controlledGear = RiftboundEffectGearTargeting.EnumerateControlledGear(session, player.PlayerIndex);
        if (controlledGear.Count < 2)
        {
            return;
        }

        card.IsExhausted = false;
    }
}

