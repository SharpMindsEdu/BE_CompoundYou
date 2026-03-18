using Domain.Entities.Riftbound;
using Domain.Simulation;
using System.Text.RegularExpressions;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class ZaunWarrensEffect : RiftboundNamedCardEffectBase
{
    public const string DiscardChoiceMarker = "-zaun-warrens-discard-";

    public override string NameIdentifier => "zaun-warrens";
    public override string TemplateId => "named.zaun-warrens";

    public override void OnConquer(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        BattlefieldState battlefield,
        string? sourceActionId
    )
    {
        var discardedCard = ResolveChosenDiscard(player, sourceActionId) ?? SelectFallbackDiscard(player);
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

    private static CardInstance? ResolveChosenDiscard(PlayerState player, string? sourceActionId)
    {
        if (string.IsNullOrWhiteSpace(sourceActionId))
        {
            return null;
        }

        var markerIndex = sourceActionId.IndexOf(DiscardChoiceMarker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return null;
        }

        var fragment = sourceActionId[(markerIndex + DiscardChoiceMarker.Length)..];
        var match = Regex.Match(
            fragment,
            @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}"
        );
        if (!match.Success || !Guid.TryParse(match.Value, out var chosenCardId))
        {
            return null;
        }

        return player.HandZone.Cards.FirstOrDefault(x => x.InstanceId == chosenCardId);
    }

    private static CardInstance? SelectFallbackDiscard(PlayerState player)
    {
        return player.HandZone.Cards
            .OrderBy(x => x.Cost.GetValueOrDefault())
            .ThenBy(x => x.Name, StringComparer.Ordinal)
            .ThenBy(x => x.InstanceId)
            .FirstOrDefault();
    }
}
