using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class WindWallEffect : RiftboundNamedCardEffectBase
{
    private const string CounterSpellMarker = "-counter-spell-";

    public override string NameIdentifier => "wind-wall";
    public override string TemplateId => "named.wind-wall";

    public override bool TryAddLegalActions(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        List<RiftboundLegalAction> actions
    )
    {
        foreach (var pending in EnumerateCounterableSpells(session, player.PlayerIndex))
        {
            actions.Add(
                new RiftboundLegalAction(
                    $"{runtime.ActionPrefix}play-{card.InstanceId}-spell{CounterSpellMarker}{pending.InstanceId}",
                    RiftboundActionType.PlayCard,
                    player.PlayerIndex,
                    $"Play {card.Name} countering {pending.Name}"
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

        targetItem.ChainItem.IsCountered = true;
        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "Resolve",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["counteredSpell"] = targetItem.Card.Name,
                ["counteredInstanceId"] = targetItem.Card.InstanceId.ToString(),
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
        return Guid.TryParse(fragment, out var parsed) ? parsed : null;
    }
}
