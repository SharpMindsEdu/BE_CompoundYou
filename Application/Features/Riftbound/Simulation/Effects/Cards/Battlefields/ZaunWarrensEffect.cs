using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class ZaunWarrensEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "zaun-warrens";
    public override string TemplateId => "named.zaun-warrens";

    public override void OnConquer(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        BattlefieldState battlefield
    )
    {
        var discardedCard = player.HandZone.Cards
            .OrderBy(x => x.Cost.GetValueOrDefault())
            .ThenBy(x => x.Name, StringComparer.Ordinal)
            .ThenBy(x => x.InstanceId)
            .FirstOrDefault();
        if (discardedCard is not null)
        {
            runtime.DiscardFromHand(
                session,
                player,
                discardedCard,
                reason: "ZaunWarrensConquer",
                sourceCard: card
            );
        }

        runtime.DrawCards(player, 1);
        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "WhenConquer",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["battlefield"] = battlefield.Name,
                ["discarded"] = (discardedCard is not null).ToString().ToLowerInvariant(),
                ["discardedCard"] = discardedCard?.Name ?? string.Empty,
                ["draw"] = "1",
            }
        );
    }
}

