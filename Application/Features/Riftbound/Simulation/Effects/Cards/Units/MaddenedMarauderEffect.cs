using Application.Features.Riftbound.Simulation.Engine;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class MaddenedMarauderEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "maddened-marauder";
    public override string TemplateId => "named.maddened-marauder";

    public override bool TryAddUnitPlayLegalActions(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        List<RiftboundLegalAction> actions
    )
    {
        var targets = session.Battlefields
            .SelectMany(x => x.Units)
            .OrderBy(x => x.Name, StringComparer.Ordinal)
            .ThenBy(x => x.InstanceId)
            .ToList();
        if (targets.Count == 0)
        {
            return true;
        }

        foreach (var target in targets)
        {
            actions.Add(
                new RiftboundLegalAction(
                    $"{runtime.ActionPrefix}play-{card.InstanceId}-target-unit-{target.InstanceId}-to-base",
                    RiftboundActionType.PlayCard,
                    player.PlayerIndex,
                    $"Play {card.Name} to base targeting {target.Name}"
                )
            );

            foreach (var battlefield in session.Battlefields.Where(x => x.ControlledByPlayerIndex == player.PlayerIndex))
            {
                actions.Add(
                    new RiftboundLegalAction(
                        $"{runtime.ActionPrefix}play-{card.InstanceId}-target-unit-{target.InstanceId}-to-bf-{battlefield.Index}",
                        RiftboundActionType.PlayCard,
                        player.PlayerIndex,
                        $"Play {card.Name} to battlefield {battlefield.Name} targeting {target.Name}"
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
        if (RiftboundEffectUnitTargeting.IsMoveToBaseLocked(session))
        {
            return;
        }

        var target = RiftboundEffectUnitTargeting.ResolveTargetUnitFromAction(session, actionId);
        if (target is null)
        {
            return;
        }

        var sourceBattlefield = RiftboundEffectUnitTargeting.FindBattlefieldContainingUnit(
            session,
            target.InstanceId
        );
        if (sourceBattlefield is null)
        {
            return;
        }

        sourceBattlefield.Units.Remove(target);
        session.Players[target.OwnerPlayerIndex].BaseZone.Cards.Add(target);
        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "WhenPlay",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["movedUnit"] = target.Name,
                ["from"] = sourceBattlefield.Name,
                ["to"] = $"base-{target.OwnerPlayerIndex}",
            }
        );
    }
}
