using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class LaurentDuelistEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "laurent-duelist";
    public override string TemplateId => "named.laurent-duelist";

    public override void OnShowdownStart(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        BattlefieldState battlefield,
        bool isAttacker,
        bool isDefender
    )
    {
        if (!isAttacker)
        {
            return;
        }

        card.TemporaryMightModifier += 2;
    }
}
