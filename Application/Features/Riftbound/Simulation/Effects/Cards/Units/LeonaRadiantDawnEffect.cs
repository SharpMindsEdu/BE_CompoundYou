using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class LeonaRadiantDawnEffect : RiftboundNamedCardEffectBase
{
    private const string TrackedTurnKey = "leona-radiant-dawn.trackedTurn";
    private const string TrackedStunnedCountKey = "leona-radiant-dawn.stunnedCount";

    public override string NameIdentifier => "leona-radiant-dawn";
    public override string TemplateId => "named.leona-radiant-dawn";

    public override void OnShowdownStart(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        BattlefieldState battlefield,
        bool isAttacker,
        bool isDefender
    )
    {
        TryTriggerFromNewStuns(runtime, session, player, card);
    }

    public override void OnFriendlyCardPlayed(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        CardInstance playedCard,
        string actionId
    )
    {
        TryTriggerFromNewStuns(runtime, session, player, card);
    }

    private static void TryTriggerFromNewStuns(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card
    )
    {
        if (
            !card.EffectData.TryGetValue(TrackedTurnKey, out var trackedTurnText)
            || !int.TryParse(trackedTurnText, out var trackedTurn)
            || trackedTurn != session.TurnNumber
        )
        {
            card.EffectData[TrackedTurnKey] = session.TurnNumber.ToString();
            card.EffectData[TrackedStunnedCountKey] = "0";
        }

        var trackedCount = 0;
        if (
            card.EffectData.TryGetValue(TrackedStunnedCountKey, out var trackedCountText)
            && int.TryParse(trackedCountText, out var parsedTrackedCount)
        )
        {
            trackedCount = parsedTrackedCount;
        }

        var enemyStunnedCount = RiftboundEffectUnitTargeting.EnumerateAllUnits(session).Count(x =>
            x.ControllerPlayerIndex != player.PlayerIndex
            && x.EffectData.TryGetValue("stunnedThisTurn", out var stunnedText)
            && bool.TryParse(stunnedText, out var stunned)
            && stunned
        );
        if (enemyStunnedCount <= trackedCount)
        {
            return;
        }

        var buffTarget = RiftboundEffectUnitTargeting.EnumerateFriendlyUnits(session, player.PlayerIndex)
            .Where(x => x.PermanentMightModifier <= 0)
            .OrderByDescending(x => runtime.GetEffectiveMight(session, x))
            .ThenBy(x => x.Name, StringComparer.Ordinal)
            .ThenBy(x => x.InstanceId)
            .FirstOrDefault();
        if (buffTarget is null)
        {
            card.EffectData[TrackedStunnedCountKey] = enemyStunnedCount.ToString();
            return;
        }

        buffTarget.PermanentMightModifier += 1;
        card.EffectData[TrackedStunnedCountKey] = enemyStunnedCount.ToString();
        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "WhenStunEnemy",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["target"] = buffTarget.Name,
                ["enemyStunnedCount"] = enemyStunnedCount.ToString(),
            }
        );
    }
}
