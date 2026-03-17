using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class DangerZoneEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "danger-zone";
    public override string TemplateId => "named.danger-zone";

    public override bool TryAddLegalActions(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        List<RiftboundLegalAction> actions
    )
    {
        var actionId = $"{runtime.ActionPrefix}play-{card.InstanceId}-spell";
        actions.Add(
            new RiftboundLegalAction(
                actionId,
                RiftboundActionType.PlayCard,
                player.PlayerIndex,
                $"Play spell {card.Name}"
            )
        );
        actions.Add(
            new RiftboundLegalAction(
                $"{actionId}{runtime.RepeatActionSuffix}",
                RiftboundActionType.PlayCard,
                player.PlayerIndex,
                $"Play spell {card.Name} (repeat)"
            )
        );
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
        BuffMechs(session, player.PlayerIndex);
        if (!runtime.IsRepeatRequested(actionId))
        {
            return;
        }

        var paid = runtime.TryPayCost(
            session,
            player,
            energyCost: 1,
            powerRequirements: [new EffectPowerRequirement(1, null)]
        );
        if (paid)
        {
            BuffMechs(session, player.PlayerIndex);
        }
    }

    private static void BuffMechs(GameSession session, int playerIndex)
    {
        foreach (var mech in RiftboundEffectUnitTargeting.EnumerateFriendlyUnits(session, playerIndex).Where(IsMech))
        {
            mech.TemporaryMightModifier += 1;
        }
    }

    private static bool IsMech(CardInstance card)
    {
        return card.Keywords.Contains("Mech", StringComparer.OrdinalIgnoreCase)
            || card.Keywords.Contains("mech", StringComparer.OrdinalIgnoreCase);
    }
}
