using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class FaePorterEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "fae-porter";
    public override string TemplateId => "named.fae-porter";

    public override void OnUnitMove(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card
    )
    {
        var destination = RiftboundEffectUnitTargeting.FindBattlefieldContainingUnit(
            session,
            card.InstanceId
        );
        if (destination is null)
        {
            return;
        }

        var candidate = RiftboundEffectUnitTargeting.EnumerateFriendlyUnits(session, player.PlayerIndex)
            .Where(x => x.InstanceId != card.InstanceId)
            .Where(x => !IsAtBattlefield(session, x.InstanceId, destination.Index))
            .OrderByDescending(x => runtime.GetEffectiveMight(session, x))
            .ThenBy(x => x.Name, StringComparer.Ordinal)
            .ThenBy(x => x.InstanceId)
            .FirstOrDefault();
        if (candidate is null)
        {
            return;
        }

        if (
            !runtime.TryPayCost(
                session,
                player,
                energyCost: 0,
                [new EffectPowerRequirement(1, ["Chaos"])]
            )
        )
        {
            return;
        }

        RemoveUnitFromCurrentLocation(session, candidate);
        destination.Units.Add(candidate);
        if (destination.ControlledByPlayerIndex != candidate.ControllerPlayerIndex)
        {
            destination.ContestedByPlayerIndex = candidate.ControllerPlayerIndex;
        }

        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "WhenMove",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["paidChaos"] = "true",
                ["movedUnit"] = candidate.Name,
                ["destination"] = destination.Name,
            }
        );
    }

    private static bool IsAtBattlefield(GameSession session, Guid unitId, int battlefieldIndex)
    {
        return session.Battlefields.Any(x =>
            x.Index == battlefieldIndex && x.Units.Any(unit => unit.InstanceId == unitId)
        );
    }

    private static void RemoveUnitFromCurrentLocation(GameSession session, CardInstance unit)
    {
        foreach (var owner in session.Players)
        {
            if (owner.BaseZone.Cards.Remove(unit))
            {
                return;
            }
        }

        foreach (var battlefield in session.Battlefields)
        {
            if (battlefield.Units.Remove(unit))
            {
                return;
            }
        }
    }
}
