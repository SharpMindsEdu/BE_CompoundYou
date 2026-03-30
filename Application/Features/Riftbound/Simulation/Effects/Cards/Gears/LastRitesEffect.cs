using System.Text.RegularExpressions;
using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed partial class LastRitesEffect : RiftboundNamedCardEffectBase
{
    public const string RecycleMarker = "-last-rites-recycle-";

    public override string NameIdentifier => "last-rites";
    public override string TemplateId => "named.last-rites";

    [GeneratedRegex(
        @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}",
        RegexOptions.Compiled
    )]
    private static partial Regex GuidRegex();

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
        var recyclePairs = BuildRecyclePairs(player.TrashZone.Cards);
        if (recyclePairs.Count == 0)
        {
            return true;
        }

        foreach (
            var target in RiftboundEffectUnitTargeting.EnumerateFriendlyUnits(
                session,
                player.PlayerIndex
            )
        )
        {
            foreach (var pair in recyclePairs)
            {
                var actionId =
                    $"{runtime.ActionPrefix}play-{card.InstanceId}-spell-target-unit-{target.InstanceId}{RecycleMarker}{pair.First.InstanceId},{pair.Second.InstanceId}";
                actions.Add(
                    new RiftboundLegalAction(
                        actionId,
                        RiftboundActionType.PlayCard,
                        player.PlayerIndex,
                        $"Play {card.Name} targeting {target.Name} [recycle {pair.First.Name} + {pair.Second.Name}]"
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
        var target = RiftboundEffectUnitTargeting.ResolveTargetUnitFromAction(session, actionId);
        if (target is null || target.ControllerPlayerIndex != player.PlayerIndex)
        {
            return;
        }

        var recycledCards = ResolveChosenRecycledCards(player, actionId);
        if (recycledCards.Count < 2)
        {
            return;
        }

        foreach (var recycle in recycledCards)
        {
            if (!player.TrashZone.Cards.Remove(recycle))
            {
                continue;
            }

            player.MainDeckZone.Cards.Add(recycle);
        }

        card.AttachedToInstanceId = target.InstanceId;
        card.IsExhausted = true;
        var battlefield = RiftboundEffectUnitTargeting.FindBattlefieldContainingUnit(
            session,
            target.InstanceId
        );
        if (battlefield is not null)
        {
            battlefield.Gear.Add(card);
        }
        else
        {
            player.BaseZone.Cards.Add(card);
        }

        runtime.NotifyGearAttached(session, card, target);
        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "Resolve",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["target"] = target.Name,
                ["recycled"] = recycledCards.Count.ToString(),
            }
        );
    }

    private static IReadOnlyList<(CardInstance First, CardInstance Second)> BuildRecyclePairs(
        IReadOnlyList<CardInstance> cards
    )
    {
        var ordered = cards
            .OrderBy(x => x.Name, StringComparer.Ordinal)
            .ThenBy(x => x.InstanceId)
            .ToList();
        if (ordered.Count < 2)
        {
            return [];
        }

        var pairs = new List<(CardInstance First, CardInstance Second)>();
        for (var i = 0; i < ordered.Count; i += 1)
        {
            for (var j = i + 1; j < ordered.Count; j += 1)
            {
                pairs.Add((ordered[i], ordered[j]));
            }
        }

        return pairs;
    }

    private static IReadOnlyList<CardInstance> ResolveChosenRecycledCards(
        PlayerState player,
        string actionId
    )
    {
        var markerIndex = actionId.IndexOf(RecycleMarker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return [];
        }

        var fragment = actionId[(markerIndex + RecycleMarker.Length)..];
        var ids = GuidRegex()
            .Matches(fragment)
            .Select(x => Guid.TryParse(x.Value, out var id) ? id : Guid.Empty)
            .Where(x => x != Guid.Empty)
            .Distinct()
            .Take(2)
            .ToList();
        if (ids.Count < 2)
        {
            return [];
        }

        var byId = player.TrashZone.Cards.ToDictionary(x => x.InstanceId);
        var chosen = new List<CardInstance>(2);
        foreach (var id in ids)
        {
            if (byId.TryGetValue(id, out var card))
            {
                chosen.Add(card);
            }
        }

        return chosen;
    }
}
