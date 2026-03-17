using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class RebukeEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "rebuke";
    public override string TemplateId => "named.rebuke";

    public override bool TryAddLegalActions(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        List<RiftboundLegalAction> actions
    )
    {
        foreach (var target in session.Battlefields.SelectMany(x => x.Units).OrderBy(x => x.Name, StringComparer.Ordinal).ThenBy(x => x.InstanceId))
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
        if (target is null)
        {
            return;
        }

        var battlefield = RiftboundEffectUnitTargeting.FindBattlefieldContainingUnit(session, target.InstanceId);
        if (battlefield is null)
        {
            return;
        }

        battlefield.Units.Remove(target);
        DetachAttachedGearToTrash(session, target.InstanceId);
        target.MarkedDamage = 0;
        target.TemporaryMightModifier = 0;
        target.IsExhausted = false;

        var owner = session.Players.FirstOrDefault(x => x.PlayerIndex == target.OwnerPlayerIndex);
        owner?.HandZone.Cards.Add(target);

        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "Resolve",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["target"] = target.Name,
                ["returnedToOwnerHand"] = owner?.PlayerIndex.ToString() ?? "unknown",
            }
        );
    }

    private static void DetachAttachedGearToTrash(GameSession session, Guid unitInstanceId)
    {
        foreach (var player in session.Players)
        {
            var attachedInBase = player.BaseZone.Cards
                .Where(c => c.AttachedToInstanceId == unitInstanceId)
                .ToList();
            foreach (var gear in attachedInBase)
            {
                player.BaseZone.Cards.Remove(gear);
                gear.AttachedToInstanceId = null;
                player.TrashZone.Cards.Add(gear);
            }
        }

        foreach (var battlefield in session.Battlefields)
        {
            var attachedGear = battlefield.Gear.Where(c => c.AttachedToInstanceId == unitInstanceId).ToList();
            foreach (var gear in attachedGear)
            {
                battlefield.Gear.Remove(gear);
                gear.AttachedToInstanceId = null;
                session.Players[gear.OwnerPlayerIndex].TrashZone.Cards.Add(gear);
            }
        }
    }
}
