using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class BeastBelowEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "beast-below";
    public override string TemplateId => "named.beast-below";

    public override bool TryAddUnitPlayLegalActions(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        List<RiftboundLegalAction> actions
    )
    {
        var destinations = new List<(string Suffix, string Description)>
        {
            ("-to-base", $"Play {card.Name} to base"),
        };
        destinations.AddRange(
            session.Battlefields
                .Where(x => x.ControlledByPlayerIndex == player.PlayerIndex)
                .OrderBy(x => x.Index)
                .Select(x => ($"-to-bf-{x.Index}", $"Play {card.Name} to battlefield {x.Name}"))
        );

        var friendlyTargets = RiftboundEffectUnitTargeting.EnumerateFriendlyUnits(session, player.PlayerIndex)
            .Where(x => x.InstanceId != card.InstanceId)
            .ToList();
        var enemyTargets = RiftboundEffectUnitTargeting.EnumerateAllUnits(session)
            .Where(x => x.ControllerPlayerIndex != player.PlayerIndex)
            .ToList();

        if (friendlyTargets.Count > 0 && enemyTargets.Count > 0)
        {
            foreach (var destination in destinations)
            {
                foreach (var friendly in friendlyTargets)
                {
                    foreach (var enemy in enemyTargets)
                    {
                        var targetList = $"{friendly.InstanceId},{enemy.InstanceId}";
                        actions.Add(
                            new RiftboundLegalAction(
                                $"{runtime.ActionPrefix}play-{card.InstanceId}-target-units-{targetList}{destination.Suffix}",
                                RiftboundActionType.PlayCard,
                                player.PlayerIndex,
                                $"{destination.Description} (return {friendly.Name} and {enemy.Name})"
                            )
                        );
                    }
                }
            }

            return true;
        }

        foreach (var destination in destinations)
        {
            actions.Add(
                new RiftboundLegalAction(
                    $"{runtime.ActionPrefix}play-{card.InstanceId}{destination.Suffix}",
                    RiftboundActionType.PlayCard,
                    player.PlayerIndex,
                    destination.Description
                )
            );
        }

        return true;
    }

    public override void OnUnitPlay(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        string actionId
    )
    {
        var selectedTargets = RiftboundEffectUnitTargeting.ResolveTargetUnitsFromAction(session, actionId).ToList();
        var friendlyTarget = selectedTargets.FirstOrDefault(x =>
            x.ControllerPlayerIndex == player.PlayerIndex && x.InstanceId != card.InstanceId
        );
        var enemyTarget = selectedTargets.FirstOrDefault(x => x.ControllerPlayerIndex != player.PlayerIndex);

        friendlyTarget ??= RiftboundEffectUnitTargeting.EnumerateFriendlyUnits(session, player.PlayerIndex)
            .FirstOrDefault(x => x.InstanceId != card.InstanceId);
        enemyTarget ??= RiftboundEffectUnitTargeting.EnumerateAllUnits(session)
            .FirstOrDefault(x => x.ControllerPlayerIndex != player.PlayerIndex);

        if (friendlyTarget is not null)
        {
            ReturnToOwnerHand(session, friendlyTarget);
        }

        if (enemyTarget is not null)
        {
            ReturnToOwnerHand(session, enemyTarget);
        }

        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "WhenPlay",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["friendlyReturned"] = friendlyTarget?.Name ?? string.Empty,
                ["enemyReturned"] = enemyTarget?.Name ?? string.Empty,
            }
        );
    }

    private static void ReturnToOwnerHand(GameSession session, CardInstance unit)
    {
        foreach (var player in session.Players)
        {
            if (player.BaseZone.Cards.Remove(unit))
            {
                break;
            }
        }

        foreach (var battlefield in session.Battlefields)
        {
            if (battlefield.Units.Remove(unit))
            {
                break;
            }
        }

        unit.ControllerPlayerIndex = unit.OwnerPlayerIndex;
        var owner = session.Players.FirstOrDefault(x => x.PlayerIndex == unit.OwnerPlayerIndex);
        owner?.HandZone.Cards.Add(unit);
    }
}

