using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class DoransBladeEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "doran-s-blade";
    public override string TemplateId => "named.doran-s-blade";

    protected override IReadOnlyDictionary<string, string> BuildData(
        RiftboundCard card,
        string normalizedEffectText
    )
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["attachedMightBonus"] = "2",
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
        foreach (var target in RiftboundEffectUnitTargeting.EnumerateFriendlyUnits(session, player.PlayerIndex))
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
        AttachToFriendlyTarget(session, player, card, actionId, runtime);
    }

    private static void AttachToFriendlyTarget(
        GameSession session,
        PlayerState player,
        CardInstance gear,
        string actionId,
        IRiftboundEffectRuntime runtime
    )
    {
        var target = RiftboundEffectUnitTargeting.ResolveTargetUnitFromAction(session, actionId);
        if (target is null || target.ControllerPlayerIndex != player.PlayerIndex)
        {
            return;
        }

        gear.AttachedToInstanceId = target.InstanceId;
        gear.IsExhausted = true;
        var battlefield = RiftboundEffectUnitTargeting.FindBattlefieldContainingUnit(session, target.InstanceId);
        if (battlefield is not null)
        {
            battlefield.Gear.Add(gear);
        }
        else
        {
            player.BaseZone.Cards.Add(gear);
        }

        runtime.NotifyGearAttached(session, gear, target);
    }
}
