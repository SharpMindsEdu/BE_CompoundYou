using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class GarenCommanderEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "garen-commander";
    public override string TemplateId => "named.garen-commander";

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
            unit.ControllerPlayerIndex != player.PlayerIndex
            || unit.InstanceId == card.InstanceId
            || !battlefield.Units.Any(x => x.InstanceId == card.InstanceId)
        )
        {
            return 0;
        }

        return 1;
    }
}

