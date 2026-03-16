using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class StackedDeckEffect : RiftboundNamedCardEffectBase
{
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
        CardInstance? drawnCard = null;
        if (remainingLooked.Count > 0)
        {
            drawnCard = ChooseCardToHandForStackedDeck(remainingLooked);
            player.MainDeckZone.Cards.Remove(drawnCard);
            player.HandZone.Cards.Add(drawnCard);
            remainingLooked.Remove(drawnCard);
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

    private static CardInstance ChooseCardToHandForStackedDeck(
        IReadOnlyCollection<CardInstance> lookedCards
    )
    {
        return lookedCards
            .OrderByDescending(x => string.Equals(x.Type, "Unit", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(x => x.Cost.GetValueOrDefault())
            .ThenByDescending(x => x.Power.GetValueOrDefault())
            .ThenByDescending(x => x.Might.GetValueOrDefault())
            .ThenBy(x => x.Name, StringComparer.Ordinal)
            .ThenBy(x => x.InstanceId)
            .First();
    }
}
