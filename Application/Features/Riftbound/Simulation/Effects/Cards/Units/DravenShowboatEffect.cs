using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class DravenShowboatEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "draven-showboat";
    public override string TemplateId => "named.draven-showboat";

    public override int GetBattlefieldUnitMightModifier(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        BattlefieldState battlefield,
        CardInstance unit
    )
    {
        if (card.InstanceId != unit.InstanceId)
        {
            return 0;
        }

        return Math.Max(0, player.Score);
    }
}

