using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class BlitzcrankImpassiveEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "blitzcrank-impassive";
    public override string TemplateId => "named.blitzcrank-impassive";

    public override bool TryAddUnitPlayLegalActions(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        List<RiftboundLegalAction> actions
    )
    {
        actions.Add(
            new RiftboundLegalAction(
                $"{runtime.ActionPrefix}play-{card.InstanceId}-to-base",
                RiftboundActionType.PlayCard,
                player.PlayerIndex,
                $"Play {card.Name} to base"
            )
        );

        var enemyUnits = RiftboundEffectUnitTargeting.EnumerateAllUnits(session)
            .Where(x => x.ControllerPlayerIndex != player.PlayerIndex)
            .ToList();

        foreach (
            var battlefield in session.Battlefields.Where(x =>
                x.ControlledByPlayerIndex == player.PlayerIndex
            )
        )
        {
            var baseAction =
                $"{runtime.ActionPrefix}play-{card.InstanceId}-to-bf-{battlefield.Index}";
            actions.Add(
                new RiftboundLegalAction(
                    baseAction,
                    RiftboundActionType.PlayCard,
                    player.PlayerIndex,
                    $"Play {card.Name} to battlefield {battlefield.Name}"
                )
            );

            foreach (var enemy in enemyUnits)
            {
                actions.Add(
                    new RiftboundLegalAction(
                        $"{runtime.ActionPrefix}play-{card.InstanceId}-target-unit-{enemy.InstanceId}-to-bf-{battlefield.Index}",
                        RiftboundActionType.PlayCard,
                        player.PlayerIndex,
                        $"Play {card.Name} to battlefield {battlefield.Name} and move {enemy.Name}"
                    )
                );
            }
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
        if (!actionId.Contains("-to-bf-", StringComparison.Ordinal))
        {
            return;
        }

        var battlefieldIndex = ResolveBattlefieldIndex(actionId);
        if (!battlefieldIndex.HasValue)
        {
            return;
        }

        var destination = session.Battlefields.FirstOrDefault(x => x.Index == battlefieldIndex.Value);
        var target = RiftboundEffectUnitTargeting.ResolveTargetUnitFromAction(session, actionId);
        if (
            destination is null
            || target is null
            || target.ControllerPlayerIndex == player.PlayerIndex
        )
        {
            return;
        }

        RemoveUnitFromCurrentLocation(session, target);
        destination.Units.Add(target);
        if (destination.ControlledByPlayerIndex != target.ControllerPlayerIndex)
        {
            destination.ContestedByPlayerIndex = target.ControllerPlayerIndex;
        }

        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "WhenPlay",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["movedUnit"] = target.Name,
                ["battlefield"] = destination.Name,
            }
        );
    }

    public override void OnHoldScore(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        BattlefieldState battlefield
    )
    {
        if (!battlefield.Units.Remove(card))
        {
            return;
        }

        var owner = session.Players.FirstOrDefault(x => x.PlayerIndex == card.OwnerPlayerIndex);
        if (owner is null)
        {
            return;
        }

        card.ControllerPlayerIndex = owner.PlayerIndex;
        owner.HandZone.Cards.Add(card);
        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "WhenHold",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["returnedToHand"] = "true",
            }
        );
    }

    private static int? ResolveBattlefieldIndex(string actionId)
    {
        var marker = "-to-bf-";
        var index = actionId.IndexOf(marker, StringComparison.Ordinal);
        if (index < 0)
        {
            return null;
        }

        var suffix = actionId[(index + marker.Length)..];
        return int.TryParse(suffix, out var parsed) ? parsed : null;
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

