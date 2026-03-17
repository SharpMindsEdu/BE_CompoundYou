using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class AltarOfMemoriesEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "altar-of-memories";
    public override string TemplateId => "named.altar-of-memories";

    public override void OnFriendlyUnitDeath(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        CardInstance deadFriendlyUnit
    )
    {
        if (card.IsExhausted)
        {
            return;
        }

        card.IsExhausted = true;
        runtime.DrawCards(player, 1);
        if (player.HandZone.Cards.Count == 0)
        {
            return;
        }

        var cardToPlace = ChooseCardToPlaceBack(player.HandZone.Cards);
        player.HandZone.Cards.Remove(cardToPlace);
        var placeTop = ShouldPlaceOnTop(cardToPlace);
        if (placeTop)
        {
            player.MainDeckZone.Cards.Insert(0, cardToPlace);
        }
        else
        {
            player.MainDeckZone.Cards.Add(cardToPlace);
        }

        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "WhenFriendlyUnitDies",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["deadUnit"] = deadFriendlyUnit.Name,
                ["drew"] = "1",
                ["placedCard"] = cardToPlace.Name,
                ["placed"] = placeTop ? "top" : "bottom",
            }
        );
    }

    private static CardInstance ChooseCardToPlaceBack(IReadOnlyCollection<CardInstance> handCards)
    {
        return handCards
            .OrderBy(x => x.Cost.GetValueOrDefault())
            .ThenBy(x => x.Power.GetValueOrDefault())
            .ThenBy(x => x.Might.GetValueOrDefault())
            .ThenBy(x => x.Name, StringComparer.Ordinal)
            .ThenBy(x => x.InstanceId)
            .First();
    }

    private static bool ShouldPlaceOnTop(CardInstance card)
    {
        return card.EffectData.Keys.Any(x =>
            x.StartsWith("topDeckReveal.", StringComparison.OrdinalIgnoreCase)
        );
    }
}
