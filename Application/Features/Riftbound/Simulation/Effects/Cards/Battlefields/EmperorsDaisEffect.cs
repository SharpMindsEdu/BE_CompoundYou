using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class EmperorsDaisEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "emperor-s-dais";
    public override string TemplateId => "named.emperor-s-dais";

    public override void OnConquer(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        BattlefieldState battlefield
    )
    {
        var unitToReturn = battlefield.Units
            .Where(x => x.ControllerPlayerIndex == player.PlayerIndex)
            .OrderByDescending(x => runtime.GetEffectiveMight(session, x))
            .ThenBy(x => x.Name, StringComparer.Ordinal)
            .ThenBy(x => x.InstanceId)
            .FirstOrDefault();
        if (unitToReturn is null)
        {
            return;
        }

        if (!runtime.TryPayCost(session, player, energyCost: 1))
        {
            return;
        }

        battlefield.Units.Remove(unitToReturn);
        session.Players[unitToReturn.OwnerPlayerIndex].HandZone.Cards.Add(unitToReturn);
        battlefield.Units.Add(
            RiftboundTokenFactory.CreateSandSoldierUnitToken(
                ownerPlayerIndex: player.PlayerIndex,
                controllerPlayerIndex: player.PlayerIndex,
                might: 2,
                exhausted: true
            )
        );
    }
}

