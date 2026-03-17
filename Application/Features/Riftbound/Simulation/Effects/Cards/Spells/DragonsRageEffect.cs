using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class DragonsRageEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "dragon-s-rage";
    public override string TemplateId => "named.dragon-s-rage";

    public override bool TryAddLegalActions(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        List<RiftboundLegalAction> actions
    )
    {
        var enemies = RiftboundEffectUnitTargeting.EnumerateAllUnits(session)
            .Where(x => x.ControllerPlayerIndex != player.PlayerIndex)
            .ToList();
        foreach (var enemy in enemies)
        {
            actions.Add(
                new RiftboundLegalAction(
                    $"{runtime.ActionPrefix}play-{card.InstanceId}-spell-target-unit-{enemy.InstanceId}-to-base",
                    RiftboundActionType.PlayCard,
                    player.PlayerIndex,
                    $"Play {card.Name} moving {enemy.Name} to base"
                )
            );

            foreach (var battlefield in session.Battlefields)
            {
                actions.Add(
                    new RiftboundLegalAction(
                        $"{runtime.ActionPrefix}play-{card.InstanceId}-spell-target-unit-{enemy.InstanceId}-to-bf-{battlefield.Index}",
                        RiftboundActionType.PlayCard,
                        player.PlayerIndex,
                        $"Play {card.Name} moving {enemy.Name} to battlefield {battlefield.Name}"
                    )
                );
            }
        }

        return true;
    }

    public override void OnSpellOrGearPlay(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        string actionId
    )
    {
        var movedUnit = RiftboundEffectUnitTargeting.ResolveTargetUnitFromAction(session, actionId);
        if (movedUnit is null || movedUnit.ControllerPlayerIndex == player.PlayerIndex)
        {
            return;
        }

        RemoveUnitFromCurrentLocation(session, movedUnit);
        if (actionId.EndsWith("-to-base", StringComparison.Ordinal))
        {
            session.Players[movedUnit.OwnerPlayerIndex].BaseZone.Cards.Add(movedUnit);
            return;
        }

        var marker = "-to-bf-";
        var markerIndex = actionId.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return;
        }

        var destinationText = actionId[(markerIndex + marker.Length)..];
        if (!int.TryParse(destinationText, out var battlefieldIndex))
        {
            return;
        }

        var destination = session.Battlefields.FirstOrDefault(x => x.Index == battlefieldIndex);
        if (destination is null)
        {
            return;
        }

        destination.Units.Add(movedUnit);
        var secondEnemy = destination.Units
            .Where(x =>
                x.ControllerPlayerIndex != player.PlayerIndex && x.InstanceId != movedUnit.InstanceId)
            .OrderByDescending(x => runtime.GetEffectiveMight(session, x))
            .ThenBy(x => x.Name, StringComparer.Ordinal)
            .ThenBy(x => x.InstanceId)
            .FirstOrDefault();
        if (secondEnemy is null)
        {
            return;
        }

        var movedMight = runtime.GetEffectiveMight(session, movedUnit);
        var secondMight = runtime.GetEffectiveMight(session, secondEnemy);
        movedUnit.MarkedDamage += secondMight;
        secondEnemy.MarkedDamage += movedMight;
    }

    private static void RemoveUnitFromCurrentLocation(GameSession session, CardInstance unit)
    {
        foreach (var player in session.Players)
        {
            if (player.BaseZone.Cards.Remove(unit))
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
