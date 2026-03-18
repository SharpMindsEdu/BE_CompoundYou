using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class StackedDeckEffect : RiftboundNamedCardEffectBase
{
    public const string PendingChoiceKind = "stacked-deck-look-choice";

    public override string NameIdentifier => "stacked-deck";
    public override string TemplateId => "named.stacked-deck";

    protected override IReadOnlyDictionary<string, string> BuildData(
        RiftboundCard card,
        string normalizedEffectText
    )
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["lookCount"] = "3",
        };
    }

    public override void OnSpellOrGearPlay(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        string actionId
    )
    {
        var lookCount = Math.Max(0, runtime.ReadIntEffectData(card, "lookCount", fallback: 3));
        var lookedCards = player.MainDeckZone.Cards.Take(lookCount).ToList();
        var playedFromReveal = new List<string>();
        var revealEnergyAdded = 0;
        foreach (var looked in lookedCards)
        {
            if (!player.MainDeckZone.Cards.Contains(looked))
            {
                continue;
            }

            var revealResolution = runtime.ResolveTopDeckRevealEffects(session, player, looked, card);
            revealEnergyAdded += revealResolution.AddedEnergy;
            if (revealResolution.PlayedCard)
            {
                playedFromReveal.Add(looked.Name);
            }
        }

        var remainingLooked = lookedCards
            .Where(x => player.MainDeckZone.Cards.Contains(x))
            .ToList();
        if (remainingLooked.Count > 1)
        {
            if (session.PendingChoice is not null)
            {
                return;
            }

            session.PendingChoice = BuildPendingChoice(
                runtime,
                player,
                card,
                remainingLooked,
                lookedCards.Count,
                playedFromReveal.Count,
                revealEnergyAdded
            );
            return;
        }

        CardInstance? drawnCard = null;
        if (remainingLooked.Count == 1)
        {
            drawnCard = remainingLooked[0];
            player.MainDeckZone.Cards.Remove(drawnCard);
            player.HandZone.Cards.Add(drawnCard);
            remainingLooked.Clear();
        }

        var recycledCount = 0;
        foreach (var cardToRecycle in remainingLooked)
        {
            if (!player.MainDeckZone.Cards.Remove(cardToRecycle))
            {
                continue;
            }

            player.MainDeckZone.Cards.Add(cardToRecycle);
            recycledCount += 1;
        }

        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["template"] = card.EffectTemplateId,
            ["looked"] = lookedCards.Count.ToString(),
            ["recycled"] = recycledCount.ToString(),
            ["repeat"] = "false",
            ["playedFromReveal"] = playedFromReveal.Count.ToString(),
            ["revealEnergyAdded"] = revealEnergyAdded.ToString(),
        };
        if (!string.IsNullOrWhiteSpace(drawnCard?.Name))
        {
            metadata["drawn"] = drawnCard.Name;
        }

        runtime.AddEffectContext(session, card.Name, player.PlayerIndex, "Resolve", metadata);
    }

    private static PendingChoiceState BuildPendingChoice(
        IRiftboundEffectRuntime runtime,
        PlayerState player,
        CardInstance sourceCard,
        IReadOnlyCollection<CardInstance> candidates,
        int lookedCount,
        int playedFromReveal,
        int revealEnergyAdded
    )
    {
        return new PendingChoiceState
        {
            Kind = PendingChoiceKind,
            PlayerIndex = player.PlayerIndex,
            SourceCardInstanceId = sourceCard.InstanceId,
            SourceCardName = sourceCard.Name,
            Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = sourceCard.EffectTemplateId,
                ["looked"] = lookedCount.ToString(),
                ["playedFromReveal"] = playedFromReveal.ToString(),
                ["revealEnergyAdded"] = revealEnergyAdded.ToString(),
                ["candidateIds"] = string.Join(",", candidates.Select(x => x.InstanceId.ToString())),
            },
            Options = candidates
                .Select(candidate => new PendingChoiceOption
                {
                    ActionId = $"{runtime.ActionPrefix}choose-stacked-deck-{candidate.InstanceId}",
                    Description = $"Stacked Deck: put {candidate.Name} into your hand",
                    Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["drawnCardId"] = candidate.InstanceId.ToString(),
                    },
                })
                .ToList(),
        };
    }

    internal static void ResolvePendingChoice(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PendingChoiceState pendingChoice,
        PendingChoiceOption option
    )
    {
        var player = session.Players.FirstOrDefault(x => x.PlayerIndex == pendingChoice.PlayerIndex);
        if (player is null)
        {
            return;
        }

        var candidates = ResolveCandidateCards(player, pendingChoice.Metadata);
        if (candidates.Count == 0)
        {
            return;
        }

        var drawnCard = ResolveChosenCard(option, candidates) ?? candidates[0];
        if (player.MainDeckZone.Cards.Remove(drawnCard))
        {
            player.HandZone.Cards.Add(drawnCard);
        }

        var recycledCount = 0;
        foreach (var cardToRecycle in candidates.Where(x => x.InstanceId != drawnCard.InstanceId))
        {
            if (!player.MainDeckZone.Cards.Remove(cardToRecycle))
            {
                continue;
            }

            player.MainDeckZone.Cards.Add(cardToRecycle);
            recycledCount += 1;
        }

        runtime.AddEffectContext(
            session,
            pendingChoice.SourceCardName,
            player.PlayerIndex,
            "Resolve",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = pendingChoice.Metadata.TryGetValue("template", out var template)
                    ? template
                    : "named.stacked-deck",
                ["looked"] = pendingChoice.Metadata.TryGetValue("looked", out var looked) ? looked : "0",
                ["recycled"] = recycledCount.ToString(),
                ["repeat"] = "false",
                ["playedFromReveal"] = pendingChoice.Metadata.TryGetValue("playedFromReveal", out var playedFromReveal)
                    ? playedFromReveal
                    : "0",
                ["revealEnergyAdded"] = pendingChoice.Metadata.TryGetValue("revealEnergyAdded", out var revealEnergyAdded)
                    ? revealEnergyAdded
                    : "0",
                ["drawn"] = drawnCard.Name,
            }
        );
    }

    private static List<CardInstance> ResolveCandidateCards(
        PlayerState player,
        IReadOnlyDictionary<string, string> metadata
    )
    {
        if (!metadata.TryGetValue("candidateIds", out var candidateIds) || string.IsNullOrWhiteSpace(candidateIds))
        {
            return [];
        }

        var candidates = new List<CardInstance>();
        foreach (
            var raw in candidateIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        )
        {
            if (!Guid.TryParse(raw, out var id))
            {
                continue;
            }

            var card = player.MainDeckZone.Cards.FirstOrDefault(x => x.InstanceId == id);
            if (card is not null)
            {
                candidates.Add(card);
            }
        }

        return candidates;
    }

    private static CardInstance? ResolveChosenCard(
        PendingChoiceOption option,
        IReadOnlyCollection<CardInstance> candidates
    )
    {
        if (
            !option.Metadata.TryGetValue("drawnCardId", out var drawnCardIdText)
            || !Guid.TryParse(drawnCardIdText, out var drawnCardId)
        )
        {
            return null;
        }

        return candidates.FirstOrDefault(x => x.InstanceId == drawnCardId);
    }
}
