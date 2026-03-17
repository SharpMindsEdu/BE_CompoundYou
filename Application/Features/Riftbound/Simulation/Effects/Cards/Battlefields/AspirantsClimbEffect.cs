using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class AspirantsClimbEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "aspirant-s-climb";
    public override string TemplateId => "named.aspirant-s-climb";

    public override int GetVictoryScoreModifier(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        CardInstance card,
        BattlefieldState battlefield
    )
    {
        return 1;
    }
}

