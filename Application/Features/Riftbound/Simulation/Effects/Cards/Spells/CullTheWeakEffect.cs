using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class CullTheWeakEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "cull-the-weak";
    public override string TemplateId => "named.cull-the-weak";

    public override bool TryAddLegalActions(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        List<RiftboundLegalAction> actions
    )
    {
        var friendlyUnits = RiftboundEffectUnitTargeting.EnumerateFriendlyUnits(session, player.PlayerIndex)
            .ToList();
        if (friendlyUnits.Count == 0)
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

        foreach (var unit in friendlyUnits)
        {
            actions.Add(
                new RiftboundLegalAction(
                    $"{runtime.ActionPrefix}play-{card.InstanceId}-spell-target-unit-{unit.InstanceId}",
                    RiftboundActionType.PlayCard,
                    player.PlayerIndex,
                    $"Play {card.Name} and kill {unit.Name}"
                )
            );
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
        var selectedFriendly = RiftboundEffectUnitTargeting.ResolveTargetUnitFromAction(session, actionId);
        if (
            selectedFriendly is not null
            && selectedFriendly.ControllerPlayerIndex != player.PlayerIndex
        )
        {
            selectedFriendly = null;
        }

        selectedFriendly ??= RiftboundEffectUnitTargeting
            .EnumerateFriendlyUnits(session, player.PlayerIndex)
            .OrderByDescending(x => runtime.GetEffectiveMight(session, x))
            .ThenBy(x => x.Name, StringComparer.Ordinal)
            .ThenBy(x => x.InstanceId)
            .FirstOrDefault();

        var opponent = session.Players.FirstOrDefault(x => x.PlayerIndex != player.PlayerIndex);
        var selectedOpponent = opponent is null
            ? null
            : RiftboundEffectUnitTargeting.EnumerateFriendlyUnits(session, opponent.PlayerIndex)
                .OrderByDescending(x => runtime.GetEffectiveMight(session, x))
                .ThenBy(x => x.Name, StringComparer.Ordinal)
                .ThenBy(x => x.InstanceId)
                .FirstOrDefault();

        KillUnit(runtime, session, selectedFriendly);
        KillUnit(runtime, session, selectedOpponent);
    }

    private static void KillUnit(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        CardInstance? unit
    )
    {
        if (unit is null)
        {
            return;
        }

        unit.MarkedDamage += Math.Max(1, runtime.GetEffectiveMight(session, unit));
    }
}
