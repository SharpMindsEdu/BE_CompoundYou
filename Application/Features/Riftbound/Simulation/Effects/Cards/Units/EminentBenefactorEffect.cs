using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class EminentBenefactorEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "eminent-benefactor";
    public override string TemplateId => "named.eminent-benefactor";

    public override void OnHoldScore(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        BattlefieldState battlefield
    )
    {
        player.BaseZone.Cards.Add(
            RiftboundTokenFactory.CreateGoldGearToken(
                ownerPlayerIndex: player.PlayerIndex,
                controllerPlayerIndex: player.PlayerIndex,
                exhausted: true
            )
        );
        player.BaseZone.Cards.Add(
            RiftboundTokenFactory.CreateGoldGearToken(
                ownerPlayerIndex: player.PlayerIndex,
                controllerPlayerIndex: player.PlayerIndex,
                exhausted: true
            )
        );
    }
}

