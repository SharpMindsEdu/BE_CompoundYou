using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class GetExcitedEffect : RiftboundNamedCardEffectBase
{
    public const string DiscardMarker = "-get-excited-discard-";

    public override string NameIdentifier => "get-excited";
    public override string TemplateId => "named.get-excited";

    public override bool TryAddLegalActions(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        List<RiftboundLegalAction> actions
    )
    {
        var discardCandidates = player.HandZone.Cards
            .Where(x => x.InstanceId != card.InstanceId)
            .ToList();
        if (discardCandidates.Count == 0)
        {
            return true;
        }

        var targets = session.Battlefields.SelectMany(x => x.Units).ToList();
        foreach (var discard in discardCandidates)
        {
            foreach (var target in targets)
            {
                actions.Add(
                    new RiftboundLegalAction(
                        $"{runtime.ActionPrefix}play-{card.InstanceId}-spell{DiscardMarker}{discard.InstanceId}-target-unit-{target.InstanceId}",
                        RiftboundActionType.PlayCard,
                        player.PlayerIndex,
                        $"Play {card.Name}: discard {discard.Name}, deal {discard.Cost.GetValueOrDefault()} to {target.Name}"
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
        var discardedCard = ResolveDiscardFromAction(player, actionId);
        var target = RiftboundEffectUnitTargeting.ResolveTargetUnitFromAction(session, actionId);
        if (
            discardedCard is null
            || target is null
            || RiftboundEffectUnitTargeting.FindBattlefieldContainingUnit(session, target.InstanceId)
                is null
        )
        {
            return;
        }

        runtime.DiscardFromHand(
            session,
            player,
            discardedCard,
            reason: "GetExcitedDiscard",
            sourceCard: card
        );
        var magnitude = Math.Max(0, discardedCard.Cost.GetValueOrDefault())
            + runtime.GetSpellAndAbilityBonusDamage(session, player.PlayerIndex);
        target.MarkedDamage += magnitude;
        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "Resolve",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["discarded"] = discardedCard.Name,
                ["target"] = target.Name,
                ["magnitude"] = magnitude.ToString(),
            }
        );
    }

    private static CardInstance? ResolveDiscardFromAction(PlayerState player, string actionId)
    {
        var markerIndex = actionId.IndexOf(DiscardMarker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return null;
        }

        var fragment = actionId[(markerIndex + DiscardMarker.Length)..];
        var guidText = fragment.Length >= 36 ? fragment[..36] : string.Empty;
        if (!Guid.TryParse(guidText, out var discardId))
        {
            return null;
        }

        return player.HandZone.Cards.FirstOrDefault(x => x.InstanceId == discardId);
    }
}

