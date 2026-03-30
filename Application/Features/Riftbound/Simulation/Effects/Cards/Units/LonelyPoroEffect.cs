using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class LonelyPoroEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "lonely-poro";
    public override string TemplateId => "named.lonely-poro";

    public override void OnDeath(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card
    )
    {
        if (!DiedAlone(session, card))
        {
            return;
        }

        runtime.DrawCards(player, 1);
        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "Deathknell",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["draw"] = "1",
                ["diedAlone"] = "true",
            }
        );
    }

    private static bool DiedAlone(GameSession session, CardInstance card)
    {
        var owner = session.Players.FirstOrDefault(x => x.PlayerIndex == card.ControllerPlayerIndex);
        if (owner?.BaseZone.Cards.Any(x => x.InstanceId == card.InstanceId) == true)
        {
            return owner.BaseZone.Cards.Count(x =>
                string.Equals(x.Type, "Unit", StringComparison.OrdinalIgnoreCase)
                && x.ControllerPlayerIndex == card.ControllerPlayerIndex
                && x.InstanceId != card.InstanceId
            ) == 0;
        }

        var battlefield = RiftboundEffectUnitTargeting.FindBattlefieldContainingUnit(
            session,
            card.InstanceId
        );
        if (battlefield is null)
        {
            return false;
        }

        return battlefield.Units.Count(x =>
            x.ControllerPlayerIndex == card.ControllerPlayerIndex && x.InstanceId != card.InstanceId
        ) == 0;
    }
}
