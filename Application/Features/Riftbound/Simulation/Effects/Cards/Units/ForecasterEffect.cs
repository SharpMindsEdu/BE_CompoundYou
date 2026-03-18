using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class ForecasterEffect : RiftboundNamedCardEffectBase
{
    public const string PendingChoiceKind = "vision-top-card-choice";

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

        if (session.PendingChoice is not null)
        {
            return;
        }

        var topCard = player.MainDeckZone.Cards.FirstOrDefault();
        if (topCard is null)
        {
            return;
        }

        session.PendingChoice = new PendingChoiceState
        {
            Kind = PendingChoiceKind,
            PlayerIndex = player.PlayerIndex,
            SourceCardInstanceId = card.InstanceId,
            SourceCardName = card.Name,
            Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["mech"] = playedCard.Name,
                ["revealedCardId"] = topCard.InstanceId.ToString(),
                ["revealed"] = topCard.Name,
                ["template"] = card.EffectTemplateId,
            },
            Options =
            [
                new PendingChoiceOption
                {
                    ActionId = $"{runtime.ActionPrefix}choose-vision-keep-{topCard.InstanceId}",
                    Description = $"Vision: keep {topCard.Name} on top of your Main Deck",
                    Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["choice"] = "keep",
                    },
                },
                new PendingChoiceOption
                {
                    ActionId = $"{runtime.ActionPrefix}choose-vision-recycle-{topCard.InstanceId}",
                    Description = $"Vision: recycle {topCard.Name} to the bottom of your Main Deck",
                    Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["choice"] = "recycle",
                    },
                },
            ],
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

        if (
            !pendingChoice.Metadata.TryGetValue("revealedCardId", out var revealedCardIdText)
            || !Guid.TryParse(revealedCardIdText, out var revealedCardId)
        )
        {
            return;
        }

        var revealedCard =
            player.MainDeckZone.Cards.FirstOrDefault(x => x.InstanceId == revealedCardId)
            ?? player.MainDeckZone.Cards.FirstOrDefault();
        if (revealedCard is null)
        {
            return;
        }

        var shouldRecycle =
            option.Metadata.TryGetValue("choice", out var choice)
            && string.Equals(choice, "recycle", StringComparison.OrdinalIgnoreCase);
        if (shouldRecycle && player.MainDeckZone.Cards.Remove(revealedCard))
        {
            player.MainDeckZone.Cards.Add(revealedCard);
        }

        runtime.AddEffectContext(
            session,
            pendingChoice.SourceCardName,
            player.PlayerIndex,
            "Vision",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = pendingChoice.Metadata.TryGetValue("template", out var template)
                    ? template
                    : "named.forecaster",
                ["mech"] = pendingChoice.Metadata.TryGetValue("mech", out var mech) ? mech : string.Empty,
                ["revealed"] = revealedCard.Name,
                ["kept"] = (!shouldRecycle).ToString().ToLowerInvariant(),
                ["recycled"] = shouldRecycle.ToString().ToLowerInvariant(),
            }
        );
    }

    private static bool IsMech(CardInstance unit)
    {
        return unit.Keywords.Contains("Mech", StringComparer.OrdinalIgnoreCase);
    }
}
