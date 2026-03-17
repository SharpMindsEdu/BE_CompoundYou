using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class DuneDrakeEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "dune-drake";
    public override string TemplateId => "named.dune-drake";

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

        var hasReadyEnemy = battlefield.Units.Any(x =>
            x.ControllerPlayerIndex != player.PlayerIndex && !x.IsExhausted
        );
        if (!hasReadyEnemy)
        {
            return;
        }

        card.TemporaryMightModifier += 2;
    }
}

