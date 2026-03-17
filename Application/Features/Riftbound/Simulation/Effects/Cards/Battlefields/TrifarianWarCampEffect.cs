using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class TrifarianWarCampEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "trifarian-war-camp";
    public override string TemplateId => "named.trifarian-war-camp";

    public override int GetBattlefieldUnitMightModifier(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        BattlefieldState battlefield,
        CardInstance unit
    )
    {
        return 1;
    }
}

