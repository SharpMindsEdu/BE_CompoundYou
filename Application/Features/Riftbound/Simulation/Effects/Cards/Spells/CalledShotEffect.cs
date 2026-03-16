using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class CalledShotEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "called-shot";
    public override string TemplateId => "named.called-shot";

    protected override IReadOnlyDictionary<string, string> BuildData(
        RiftboundCard card,
        string normalizedEffectText
    )
    {
        var data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["lookCount"] = "2",
        };

        var repeatSection = RiftboundEffectTextParser.TryExtractRepeatSection(normalizedEffectText);
        if (string.IsNullOrWhiteSpace(repeatSection))
        {
            return data;
        }

        var repeatEnergy = RiftboundEffectTextParser.TryExtractEnergyIconValue(repeatSection);
        if (repeatEnergy.HasValue && repeatEnergy.Value > 0)
        {
            data["repeatEnergyCost"] = repeatEnergy.Value.ToString();
        }

        var repeatDomain =
            RiftboundEffectTextParser.TryExtractRuneDomain(repeatSection)
            ?? RiftboundEffectTextParser.TryExtractBracketDomain(repeatSection)
            ?? card.Color?.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

        if (!string.IsNullOrWhiteSpace(repeatDomain))
        {
            data[$"repeatPowerCost.{RiftboundEffectTextParser.NormalizeDomain(repeatDomain)}"] = "1";
        }

        return data;
    }

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

        var hasRepeatCost =
            runtime.ReadIntEffectData(card, "repeatEnergyCost", fallback: 0) > 0
            || card.EffectData.Keys.Any(key =>
                key.StartsWith("repeatPowerCost.", StringComparison.OrdinalIgnoreCase)
            );
        if (hasRepeatCost)
        {
            actions.Add(
                new RiftboundLegalAction(
                    $"{actionId}{runtime.RepeatActionSuffix}",
                    RiftboundActionType.PlayCard,
                    player.PlayerIndex,
                    $"Play spell {card.Name} (repeat)"
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
        ResolveOnce(runtime, session, player, card, repeat: false);

        if (!runtime.IsRepeatRequested(actionId))
        {
            return;
        }

        if (!runtime.TryPayRepeatCost(session, player, card))
        {
            return;
        }

        ResolveOnce(runtime, session, player, card, repeat: true);
    }

    private static void ResolveOnce(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        bool repeat
    )
    {
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
        CardInstance? drawnCard = null;
        if (remainingLooked.Count > 0)
        {
            drawnCard = ChooseCardToDrawForCalledShot(remainingLooked);
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
            ["repeat"] = repeat ? "true" : "false",
            ["playedFromReveal"] = playedFromReveal.Count.ToString(),
            ["revealEnergyAdded"] = revealEnergyAdded.ToString(),
        };
        if (!string.IsNullOrWhiteSpace(drawnCard?.Name))
        {
            metadata["drawn"] = drawnCard.Name;
        }

        runtime.AddEffectContext(session, card.Name, player.PlayerIndex, "Resolve", metadata);
    }

    private static CardInstance ChooseCardToDrawForCalledShot(
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
