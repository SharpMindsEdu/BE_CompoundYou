using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class HardBargainEffect : RiftboundNamedCardEffectBase
{
    private const string CounterSpellMarker = "-counter-spell-";

    public override string NameIdentifier => "hard-bargain";
    public override string TemplateId => "named.hard-bargain";

    protected override IReadOnlyDictionary<string, string> BuildData(
        RiftboundCard card,
        string normalizedEffectText
    )
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["unlessPayEnergy"] = "2",
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
        var hasRepeat = runtime.ReadIntEffectData(card, "repeatEnergyCost", fallback: 0) > 0;
        foreach (var pending in EnumerateCounterableSpells(session, player.PlayerIndex))
        {
            var actionId =
                $"{runtime.ActionPrefix}play-{card.InstanceId}-spell{CounterSpellMarker}{pending.InstanceId}";
            actions.Add(
                new RiftboundLegalAction(
                    actionId,
                    RiftboundActionType.PlayCard,
                    player.PlayerIndex,
                    $"Play {card.Name} countering {pending.Name}"
                )
            );

            if (hasRepeat)
            {
                actions.Add(
                    new RiftboundLegalAction(
                        $"{actionId}{runtime.RepeatActionSuffix}",
                        RiftboundActionType.PlayCard,
                        player.PlayerIndex,
                        $"Play {card.Name} countering {pending.Name} (repeat)"
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
        ResolveOnce(runtime, session, player, card, actionId, repeat: false);

        if (!runtime.IsRepeatRequested(actionId))
        {
            return;
        }

        if (!runtime.TryPayRepeatCost(session, player, card))
        {
            return;
        }

        ResolveOnce(runtime, session, player, card, actionId, repeat: true);
    }

    private static void ResolveOnce(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance sourceCard,
        string actionId,
        bool repeat
    )
    {
        var targetId = ResolveCounterTargetFromAction(actionId);
        var targetItem = session.Chain
            .Where(x => x.IsPending && !x.IsCountered && x.Kind == "PlayCard")
            .Where(x => x.ControllerPlayerIndex != player.PlayerIndex)
            .Where(x => targetId is null || x.CardInstanceId == targetId.Value)
            .Select(x => new
            {
                ChainItem = x,
                Card = RiftboundEffectCardLookup.FindCardByInstanceId(session, x.CardInstanceId),
            })
            .Where(x =>
                x.Card is not null
                && string.Equals(x.Card.Type, "Spell", StringComparison.OrdinalIgnoreCase)
            )
            .FirstOrDefault();
        if (targetItem is null || targetItem.Card is null)
        {
            return;
        }

        var unlessPayEnergy = runtime.ReadIntEffectData(sourceCard, "unlessPayEnergy", fallback: 2);
        var controller = session.Players.FirstOrDefault(x =>
            x.PlayerIndex == targetItem.ChainItem.ControllerPlayerIndex
        );
        var paid = false;
        if (controller is not null && unlessPayEnergy > 0 && controller.RunePool.Energy >= unlessPayEnergy)
        {
            controller.RunePool.Energy -= unlessPayEnergy;
            paid = true;
        }
        else
        {
            targetItem.ChainItem.IsCountered = true;
        }

        runtime.AddEffectContext(
            session,
            sourceCard.Name,
            player.PlayerIndex,
            "Resolve",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = sourceCard.EffectTemplateId,
                ["counteredSpell"] = targetItem.Card.Name,
                ["countered"] = paid ? "false" : "true",
                ["paidEnergy"] = paid ? unlessPayEnergy.ToString() : "0",
                ["repeat"] = repeat ? "true" : "false",
            }
        );
    }

    private static IReadOnlyCollection<CardInstance> EnumerateCounterableSpells(
        GameSession session,
        int actingPlayerIndex
    )
    {
        return session.Chain
            .Where(x => x.IsPending && !x.IsCountered && x.Kind == "PlayCard")
            .Where(x => x.ControllerPlayerIndex != actingPlayerIndex)
            .Select(x => RiftboundEffectCardLookup.FindCardByInstanceId(session, x.CardInstanceId))
            .Where(x =>
                x is not null
                && string.Equals(x.Type, "Spell", StringComparison.OrdinalIgnoreCase)
            )
            .Cast<CardInstance>()
            .DistinctBy(x => x.InstanceId)
            .OrderBy(x => x.Name, StringComparer.Ordinal)
            .ThenBy(x => x.InstanceId)
            .ToList();
    }

    private static Guid? ResolveCounterTargetFromAction(string actionId)
    {
        var markerIndex = actionId.IndexOf(CounterSpellMarker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return null;
        }

        var fragment = actionId[(markerIndex + CounterSpellMarker.Length)..];
        if (fragment.EndsWith("-repeat", StringComparison.Ordinal))
        {
            fragment = fragment[..^"-repeat".Length];
        }

        return Guid.TryParse(fragment, out var parsed) ? parsed : null;
    }
}
