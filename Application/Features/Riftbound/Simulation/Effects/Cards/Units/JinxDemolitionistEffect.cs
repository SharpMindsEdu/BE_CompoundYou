using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class JinxDemolitionistEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "jinx-demolitionist";
    public override string TemplateId => "named.jinx-demolitionist";

    public override void OnUnitPlay(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        string actionId
    )
    {
        var discardedCards = player.HandZone.Cards
            .OrderBy(x => x.Cost.GetValueOrDefault())
            .ThenBy(x => x.Name, StringComparer.Ordinal)
            .ThenBy(x => x.InstanceId)
            .Take(2)
            .ToList();
        if (discardedCards.Count == 0)
        {
            return;
        }

        foreach (var discarded in discardedCards)
        {
            runtime.DiscardFromHand(
                session,
                player,
                discarded,
                reason: "jinx-demolitionist-play",
                sourceCard: card
            );
        }

        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "WhenPlay",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["discarded"] = discardedCards.Count.ToString(),
            }
        );
    }
}
