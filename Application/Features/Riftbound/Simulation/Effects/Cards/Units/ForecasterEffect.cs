using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class ForecasterEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "forecaster";
    public override string TemplateId => "named.forecaster";

    protected override IReadOnlyCollection<string> BuildKeywords(
        RiftboundCard card,
        string normalizedEffectText,
        IReadOnlySet<string> baseKeywords
    )
    {
        var keywords = baseKeywords.ToList();
        if (!keywords.Contains("Mech", StringComparer.OrdinalIgnoreCase))
        {
            keywords.Add("Mech");
        }

        return keywords
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public override bool GrantsKeywordToFriendlyUnit(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        CardInstance targetUnit,
        string keyword
    )
    {
        if (targetUnit.ControllerPlayerIndex != player.PlayerIndex || !IsMech(targetUnit))
        {
            return false;
        }

        return string.Equals(keyword, "Vision", StringComparison.OrdinalIgnoreCase);
    }

    public override void OnFriendlyCardPlayed(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        CardInstance playedCard,
        string actionId
    )
    {
        if (playedCard.ControllerPlayerIndex != player.PlayerIndex || !IsMech(playedCard))
        {
            return;
        }

        var topCard = player.MainDeckZone.Cards.FirstOrDefault();
        if (topCard is null)
        {
            return;
        }

        var revealedName = topCard.Name;
        var isGear = string.Equals(topCard.Type, "Gear", StringComparison.OrdinalIgnoreCase);
        if (isGear)
        {
            player.MainDeckZone.Cards.Remove(topCard);
            player.HandZone.Cards.Add(topCard);
        }
        else
        {
            player.MainDeckZone.Cards.Remove(topCard);
            player.MainDeckZone.Cards.Add(topCard);
        }

        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "Vision",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["mech"] = playedCard.Name,
                ["revealed"] = revealedName,
                ["drew"] = isGear ? "true" : "false",
                ["recycled"] = isGear ? "false" : "true",
            }
        );
    }

    private static bool IsMech(CardInstance unit)
    {
        return unit.Keywords.Contains("Mech", StringComparer.OrdinalIgnoreCase);
    }
}
