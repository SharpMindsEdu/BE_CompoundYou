using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class FlashEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "flash";
    public override string TemplateId => "named.flash";

    public override bool TryAddLegalActions(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        List<RiftboundLegalAction> actions
    )
    {
        var units = session.Battlefields.SelectMany(x => x.Units)
            .Where(x => x.ControllerPlayerIndex == player.PlayerIndex)
            .OrderBy(x => x.Name, StringComparer.Ordinal)
            .ThenBy(x => x.InstanceId)
            .ToList();
        if (units.Count == 0)
        {
            actions.Add(
                new RiftboundLegalAction(
                    $"{runtime.ActionPrefix}play-{card.InstanceId}-spell",
                    RiftboundActionType.PlayCard,
                    player.PlayerIndex,
                    $"Play spell {card.Name}"
                )
            );
            return true;
        }

        foreach (var unit in units)
        {
            actions.Add(
                new RiftboundLegalAction(
                    $"{runtime.ActionPrefix}play-{card.InstanceId}-spell-target-unit-{unit.InstanceId}",
                    RiftboundActionType.PlayCard,
                    player.PlayerIndex,
                    $"Play {card.Name} moving {unit.Name} to base"
                )
            );
        }

        for (var left = 0; left < units.Count; left += 1)
        {
            for (var right = left + 1; right < units.Count; right += 1)
            {
                var ids = $"{units[left].InstanceId},{units[right].InstanceId}";
                actions.Add(
                    new RiftboundLegalAction(
                        $"{runtime.ActionPrefix}play-{card.InstanceId}-spell{runtime.MultiTargetUnitsMarker}{ids}",
                        RiftboundActionType.PlayCard,
                        player.PlayerIndex,
                        $"Play {card.Name} moving {units[left].Name} and {units[right].Name} to base"
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
        if (RiftboundEffectUnitTargeting.IsMoveToBaseLocked(session))
        {
            return;
        }

        var targets = RiftboundEffectUnitTargeting.ResolveTargetUnitsFromAction(session, actionId)
            .Where(x => x.ControllerPlayerIndex == player.PlayerIndex)
            .ToList();
        if (targets.Count == 0)
        {
            var single = RiftboundEffectUnitTargeting.ResolveTargetUnitFromAction(session, actionId);
            if (single is not null && single.ControllerPlayerIndex == player.PlayerIndex)
            {
                targets.Add(single);
            }
        }

        foreach (var target in targets.DistinctBy(x => x.InstanceId).Take(2))
        {
            var fromBattlefield = session.Battlefields.FirstOrDefault(x =>
                x.Units.Any(unit => unit.InstanceId == target.InstanceId)
            );
            if (fromBattlefield is null)
            {
                continue;
            }

            fromBattlefield.Units.Remove(target);
            session.Players[target.OwnerPlayerIndex].BaseZone.Cards.Add(target);
        }
    }
}
