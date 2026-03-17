using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class RekSaiSwarmQueenEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "rek-sai-swarm-queen";
    public override string TemplateId => "named.rek-sai-swarm-queen";
    public override bool HasActivatedAbility => true;

    protected override IReadOnlyDictionary<string, string> BuildData(
        RiftboundCard card,
        string normalizedEffectText
    )
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["lookCount"] = "2",
        };
    }

    public override bool TryActivateAbility(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card
    )
    {
        var battlefield = RiftboundEffectUnitTargeting.FindBattlefieldContainingUnit(session, card.InstanceId);
        if (battlefield is null)
        {
            return false;
        }

        card.IsExhausted = true;
        var lookCount = Math.Max(0, runtime.ReadIntEffectData(card, "lookCount", fallback: 2));
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
        CardInstance? playedByAbility = null;
        foreach (var candidate in RankRevealPlayCandidates(remainingLooked))
        {
            if (
                !runtime.TryPlayCardFromReveal(
                    session,
                    player,
                    candidate,
                    card,
                    energyCostReduction: 0,
                    preferredBattlefieldIndex: battlefield.Index
                )
            )
            {
                continue;
            }

            playedByAbility = candidate;
            playedFromReveal.Add(candidate.Name);
            remainingLooked.Remove(candidate);
            break;
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
            ["playedFromReveal"] = playedFromReveal.Count.ToString(),
            ["revealEnergyAdded"] = revealEnergyAdded.ToString(),
            ["battlefield"] = battlefield.Index.ToString(),
        };
        if (!string.IsNullOrWhiteSpace(playedByAbility?.Name))
        {
            metadata["played"] = playedByAbility.Name;
        }

        runtime.AddEffectContext(session, card.Name, player.PlayerIndex, "WhenAttack", metadata);
        return true;
    }

    private static IReadOnlyList<CardInstance> RankRevealPlayCandidates(
        IReadOnlyCollection<CardInstance> cards
    )
    {
        return cards
            .OrderByDescending(x => string.Equals(x.Type, "Unit", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(x => x.Cost.GetValueOrDefault())
            .ThenByDescending(x => x.Power.GetValueOrDefault())
            .ThenByDescending(x => x.Might.GetValueOrDefault())
            .ThenBy(x => x.Name, StringComparer.Ordinal)
            .ThenBy(x => x.InstanceId)
            .ToList();
    }
}
