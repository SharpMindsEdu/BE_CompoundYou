using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class CorinaVerazaEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "corina-veraza";
    public override string TemplateId => "named.corina-veraza";

    public override void OnUnitMove(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card
    )
    {
        var battlefield = RiftboundEffectUnitTargeting.FindBattlefieldContainingUnit(session, card.InstanceId);
        if (battlefield is null)
        {
            return;
        }

        for (var i = 0; i < 3; i += 1)
        {
            battlefield.Units.Add(
                RiftboundTokenFactory.CreateRecruitUnitToken(
                    ownerPlayerIndex: player.PlayerIndex,
                    controllerPlayerIndex: player.PlayerIndex,
                    might: 1,
                    exhausted: true
                )
            );
        }
    }
}

