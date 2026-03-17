using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class FacebreakerEffect : RiftboundNamedCardEffectBase
{
    public const string FriendlyMarker = "-facebreaker-friendly-";
    public const string EnemyMarker = "-facebreaker-enemy-";

    public override string NameIdentifier => "facebreaker";
    public override string TemplateId => "named.facebreaker";

    public override bool TryAddLegalActions(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        List<RiftboundLegalAction> actions
    )
    {
        foreach (var battlefield in session.Battlefields)
        {
            var friendly = battlefield.Units.Where(x => x.ControllerPlayerIndex == player.PlayerIndex).ToList();
            var enemies = battlefield.Units.Where(x => x.ControllerPlayerIndex != player.PlayerIndex).ToList();
            foreach (var friendlyUnit in friendly)
            {
                foreach (var enemyUnit in enemies)
                {
                    actions.Add(
                        new RiftboundLegalAction(
                            $"{runtime.ActionPrefix}play-{card.InstanceId}-spell{FriendlyMarker}{friendlyUnit.InstanceId}{EnemyMarker}{enemyUnit.InstanceId}",
                            RiftboundActionType.PlayCard,
                            player.PlayerIndex,
                            $"Play {card.Name} stunning {friendlyUnit.Name} and {enemyUnit.Name}"
                        )
                    );
                }
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
        )
        {
            return;
        }

        friendly.IsExhausted = true;
        enemy.IsExhausted = true;
        friendly.EffectData["stunnedThisTurn"] = "true";
        enemy.EffectData["stunnedThisTurn"] = "true";
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

        return RiftboundEffectUnitTargeting.EnumerateAllUnits(session).FirstOrDefault(x => x.InstanceId == id);
    }
}

