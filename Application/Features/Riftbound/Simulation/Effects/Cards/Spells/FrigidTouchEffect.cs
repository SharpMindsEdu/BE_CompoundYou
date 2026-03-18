using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class FrigidTouchEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "frigid-touch";
    public override string TemplateId => "named.frigid-touch";

    protected override IReadOnlyDictionary<string, string> BuildData(
        RiftboundCard card,
        string normalizedEffectText
    )
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["repeatEnergyCost"] = "2",
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
        foreach (var target in RiftboundEffectUnitTargeting.EnumerateAllUnits(session))
        {
            var actionId = $"{runtime.ActionPrefix}play-{card.InstanceId}-spell-target-unit-{target.InstanceId}";
            actions.Add(
                new RiftboundLegalAction(
                    actionId,
                    RiftboundActionType.PlayCard,
                    player.PlayerIndex,
                    $"Play {card.Name} targeting {target.Name}"
                )
            );
            actions.Add(
                new RiftboundLegalAction(
                    $"{actionId}{runtime.RepeatActionSuffix}",
                    RiftboundActionType.PlayCard,
                    player.PlayerIndex,
                    $"Play {card.Name} targeting {target.Name} (repeat)"
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
        if (target is null)
        {
            return;
        }

        target.TemporaryMightModifier -= 2;
        if (runtime.IsRepeatRequested(actionId) && runtime.TryPayRepeatCost(session, player, card))
        {
            target.TemporaryMightModifier -= 2;
        }
    }
}
