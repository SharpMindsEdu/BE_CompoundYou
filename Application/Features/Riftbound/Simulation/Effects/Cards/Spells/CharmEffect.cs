using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class CharmEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "charm";
    public override string TemplateId => "named.charm";

    public override bool TryAddLegalActions(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        List<RiftboundLegalAction> actions
    )
    {
        var enemyUnits = RiftboundEffectUnitTargeting.EnumerateAllUnits(session)
            .Where(x => x.ControllerPlayerIndex != player.PlayerIndex)
            .ToList();
        foreach (var enemy in enemyUnits)
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
        var target = RiftboundEffectUnitTargeting.ResolveTargetUnitFromAction(session, actionId);
        if (target is null || target.ControllerPlayerIndex == player.PlayerIndex)
        {
            return;
        }

        RemoveUnitFromCurrentLocation(session, target);
        if (actionId.EndsWith("-to-base", StringComparison.Ordinal))
        {
            var owner = session.Players[target.OwnerPlayerIndex];
            owner.BaseZone.Cards.Add(target);
            return;
        }

        var marker = "-to-bf-";
        var index = actionId.IndexOf(marker, StringComparison.Ordinal);
        if (index < 0)
        {
            return;
        }

        if (!int.TryParse(actionId[(index + marker.Length)..], out var battlefieldIndex))
        {
            return;
        }

        var destination = session.Battlefields.FirstOrDefault(x => x.Index == battlefieldIndex);
        if (destination is null)
        {
            return;
        }

        destination.Units.Add(target);
        if (destination.ControlledByPlayerIndex != target.ControllerPlayerIndex)
        {
            destination.ContestedByPlayerIndex = target.ControllerPlayerIndex;
        }
    }

    private static void RemoveUnitFromCurrentLocation(GameSession session, CardInstance unit)
    {
        foreach (var currentPlayer in session.Players)
        {
            if (currentPlayer.BaseZone.Cards.Remove(unit))
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

