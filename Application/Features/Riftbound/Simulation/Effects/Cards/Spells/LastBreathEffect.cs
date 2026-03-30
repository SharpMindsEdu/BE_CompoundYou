using Application.Features.Riftbound.Simulation.Engine;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class LastBreathEffect : RiftboundNamedCardEffectBase
{
    private const string FriendlyMarker = "-last-breath-friendly-";
    private const string EnemyMarker = "-last-breath-enemy-";

    public override string NameIdentifier => "last-breath";
    public override string TemplateId => "named.last-breath";

    public override bool TryAddLegalActions(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        List<RiftboundLegalAction> actions
    )
    {
        var friendlyUnits = RiftboundEffectUnitTargeting.EnumerateFriendlyUnits(
            session,
            player.PlayerIndex
        );
        var enemyBattlefieldUnits = session.Battlefields
            .SelectMany(x => x.Units)
            .Where(x => x.ControllerPlayerIndex != player.PlayerIndex);

        foreach (var friendly in friendlyUnits)
        {
            foreach (var enemy in enemyBattlefieldUnits)
            {
                actions.Add(
                    new RiftboundLegalAction(
                        $"{runtime.ActionPrefix}play-{card.InstanceId}-spell{FriendlyMarker}{friendly.InstanceId}{EnemyMarker}{enemy.InstanceId}",
                        RiftboundActionType.PlayCard,
                        player.PlayerIndex,
                        $"Play {card.Name}: ready {friendly.Name} and deal damage to {enemy.Name}"
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
        var friendly = ResolveTarget(session, actionId, FriendlyMarker);
        var enemy = ResolveTarget(session, actionId, EnemyMarker);
        if (
            friendly is null
            || enemy is null
            || friendly.ControllerPlayerIndex != player.PlayerIndex
            || enemy.ControllerPlayerIndex == player.PlayerIndex
            || RiftboundEffectUnitTargeting.FindBattlefieldContainingUnit(session, enemy.InstanceId) is null
        )
        {
            return;
        }

        friendly.IsExhausted = false;
        enemy.MarkedDamage += runtime.GetEffectiveMight(session, friendly);
    }

    private static CardInstance? ResolveTarget(GameSession session, string actionId, string marker)
    {
        var markerIndex = actionId.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return null;
        }

        var fragment = actionId[(markerIndex + marker.Length)..];
        var guidText = fragment.Length >= 36 ? fragment[..36] : string.Empty;
        if (!Guid.TryParse(guidText, out var id))
        {
            return null;
        }

        return RiftboundEffectUnitTargeting.EnumerateAllUnits(session).FirstOrDefault(x =>
            x.InstanceId == id
        );
    }
}

