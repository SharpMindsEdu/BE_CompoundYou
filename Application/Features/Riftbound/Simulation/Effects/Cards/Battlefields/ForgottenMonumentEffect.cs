using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class ForgottenMonumentEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "forgotten-monument";
    public override string TemplateId => "named.forgotten-monument";

    public override bool CanPlayerScoreAtBattlefield(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        CardInstance card,
        BattlefieldState battlefield,
        PlayerState scoringPlayer
    )
    {
        return session.TurnNumber >= 3;
    }
}
