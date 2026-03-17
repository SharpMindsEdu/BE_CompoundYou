using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class VoidRushEffect : RiftboundNamedCardEffectBase
{
    private const string PlayChoiceMarker = "-voidrush-play-";
    private const string SkipChoiceSuffix = "-voidrush-skip";
    private const string AccelerateChoiceSuffix = "-accelerate";

    public override string NameIdentifier => "void-rush";
    public override string TemplateId => "named.void-rush";

    protected override IReadOnlyDictionary<string, string> BuildData(
        RiftboundCard card,
        string normalizedEffectText
    )
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["lookCount"] = "2",
            ["playEnergyReduction"] = "2",
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
        var actionIdBase = $"{runtime.ActionPrefix}play-{card.InstanceId}-spell";
        actions.Add(
            new RiftboundLegalAction(
                actionIdBase,
                RiftboundActionType.PlayCard,
                player.PlayerIndex,
                $"Play spell {card.Name} (draw all revealed cards)"
            )
        );
        actions.Add(
            new RiftboundLegalAction(
                $"{actionIdBase}{SkipChoiceSuffix}",
                RiftboundActionType.PlayCard,
                player.PlayerIndex,
                $"Play spell {card.Name} (skip reveal play)"
            )
        );

        var lookCount = Math.Max(0, runtime.ReadIntEffectData(card, "lookCount", fallback: 2));
        var lookedCards = player.MainDeckZone.Cards.Take(lookCount).ToList();
        foreach (var looked in lookedCards)
        {
            var playActionId = $"{actionIdBase}{PlayChoiceMarker}{looked.InstanceId}";
            actions.Add(
                new RiftboundLegalAction(
                    playActionId,
                    RiftboundActionType.PlayCard,
                    player.PlayerIndex,
                    $"Play {card.Name}: play revealed {looked.Name}"
                )
            );

            if (!CanUseAccelerateFromReveal(session, player.PlayerIndex, looked))
            {
                continue;
            }

            actions.Add(
                new RiftboundLegalAction(
                    $"{playActionId}{AccelerateChoiceSuffix}",
                    RiftboundActionType.PlayCard,
                    player.PlayerIndex,
                    $"Play {card.Name}: play revealed {looked.Name} with accelerate"
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
        var lookCount = Math.Max(0, runtime.ReadIntEffectData(card, "lookCount", fallback: 2));
        var playEnergyReduction = Math.Max(
            0,
            runtime.ReadIntEffectData(card, "playEnergyReduction", fallback: 2)
        );
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
        var chosenCardId = ParseChosenRevealCardId(actionId);
        var accelerateRequested = actionId.EndsWith(
            AccelerateChoiceSuffix,
            StringComparison.Ordinal
        );
        CardInstance? playedByVoidRush = null;
        if (chosenCardId.HasValue)
        {
            var chosenCard = remainingLooked.FirstOrDefault(x => x.InstanceId == chosenCardId.Value);
            if (
                chosenCard is not null
                && runtime.TryPlayCardFromReveal(
                    session,
                    player,
                    chosenCard,
                    card,
                    energyCostReduction: playEnergyReduction,
                    payAccelerateAdditionalCost: accelerateRequested
                )
            )
            {
                playedByVoidRush = chosenCard;
                playedFromReveal.Add(chosenCard.Name);
                remainingLooked.Remove(chosenCard);
            }
        }

        var drawnCount = 0;
        foreach (var cardToDraw in remainingLooked)
        {
            if (!player.MainDeckZone.Cards.Remove(cardToDraw))
            {
                continue;
            }

            player.HandZone.Cards.Add(cardToDraw);
            drawnCount += 1;
        }

        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["template"] = card.EffectTemplateId,
            ["looked"] = lookedCards.Count.ToString(),
            ["drawn"] = drawnCount.ToString(),
            ["playedFromReveal"] = playedFromReveal.Count.ToString(),
            ["revealEnergyAdded"] = revealEnergyAdded.ToString(),
            ["playEnergyReduction"] = playEnergyReduction.ToString(),
        };
        if (!string.IsNullOrWhiteSpace(playedByVoidRush?.Name))
        {
            metadata["played"] = playedByVoidRush.Name;
        }

        runtime.AddEffectContext(session, card.Name, player.PlayerIndex, "Resolve", metadata);
    }

    private static Guid? ParseChosenRevealCardId(string actionId)
    {
        var markerIndex = actionId.IndexOf(PlayChoiceMarker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return null;
        }

        var fragment = actionId[(markerIndex + PlayChoiceMarker.Length)..];
        if (fragment.EndsWith(AccelerateChoiceSuffix, StringComparison.Ordinal))
        {
            fragment = fragment[..^AccelerateChoiceSuffix.Length];
        }

        return Guid.TryParse(fragment, out var parsed) ? parsed : null;
    }

    private static bool CanUseAccelerateFromReveal(
        GameSession session,
        int playerIndex,
        CardInstance revealedCard
    )
    {
        if (!string.Equals(revealedCard.Type, "Unit", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (revealedCard.Keywords.Contains("Accelerate", StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        return RiftboundEffectUnitTargeting.EnumerateFriendlyUnits(session, playerIndex).Any(unit =>
            unit.EffectData.TryGetValue("grantAccelerateForNonHandPlay", out var raw)
            && bool.TryParse(raw, out var parsed)
            && parsed
        );
    }
}
