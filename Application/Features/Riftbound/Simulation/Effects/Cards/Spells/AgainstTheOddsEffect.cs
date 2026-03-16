using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class AgainstTheOddsEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "against-the-odds";
    public override string TemplateId => "named.against-the-odds";

    protected override IReadOnlyDictionary<string, string> BuildData(
        RiftboundCard card,
        string normalizedEffectText
    )
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["magnitudePerEnemyUnit"] = (RiftboundEffectTextParser.TryExtractMagnitude(normalizedEffectText) ?? 2).ToString(),
        };
    }

    public override bool TryAddLegalActions(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        List<RiftboundLegalAction> actions
    )
    {
        foreach (
            var target in RiftboundEffectUnitTargeting.EnumerateFriendlyBattlefieldUnits(
                session,
                player.PlayerIndex
            )
        )
        {
            actions.Add(
                new RiftboundLegalAction(
                    $"{runtime.ActionPrefix}play-{card.InstanceId}-spell-target-unit-{target.InstanceId}",
                    RiftboundActionType.PlayCard,
                    player.PlayerIndex,
                    $"Play {card.Name} targeting {target.Name}"
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
        var target = RiftboundEffectUnitTargeting.ResolveTargetUnitFromAction(session, actionId);
        if (target is null || target.ControllerPlayerIndex != player.PlayerIndex)
        {
            return;
        }

        var battlefield = RiftboundEffectUnitTargeting.FindBattlefieldContainingUnit(
            session,
            target.InstanceId
        );
        if (battlefield is null)
        {
            return;
        }

        var enemyUnitsThere = battlefield.Units.Count(x => x.ControllerPlayerIndex != player.PlayerIndex);
        var magnitudePerEnemy = runtime.ReadIntEffectData(card, "magnitudePerEnemyUnit", fallback: 2);
        var totalBuff = enemyUnitsThere * Math.Max(0, magnitudePerEnemy);
        target.TemporaryMightModifier += totalBuff;

        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "Resolve",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["target"] = target.Name,
                ["battlefield"] = battlefield.Name,
                ["enemyUnits"] = enemyUnitsThere.ToString(),
                ["magnitudePerEnemy"] = magnitudePerEnemy.ToString(),
                ["totalBuff"] = totalBuff.ToString(),
            }
        );
    }
}
