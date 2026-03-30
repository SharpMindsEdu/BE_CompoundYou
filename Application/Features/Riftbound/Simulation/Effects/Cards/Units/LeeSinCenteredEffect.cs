using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class LeeSinCenteredEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "lee-sin-centered";
    public override string TemplateId => "named.lee-sin-centered";

    public override int GetBattlefieldUnitMightModifier(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        BattlefieldState battlefield,
        CardInstance unit
    )
    {
        if (
            !battlefield.Units.Any(x => x.InstanceId == card.InstanceId)
            || unit.ControllerPlayerIndex != player.PlayerIndex
            || unit.InstanceId == card.InstanceId
            || unit.PermanentMightModifier <= 0
        )
        {
            return 0;
        }

        return 2;
    }
}
