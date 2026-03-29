using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class GemcraftSeerEffect : RiftboundNamedCardEffectBase
{
    public const string PendingChoiceKind = "gemcraft-seer-vision-choice";

    public override string NameIdentifier => "gemcraft-seer";
    public override string TemplateId => "named.gemcraft-seer";

    public override bool GrantsKeywordToFriendlyUnit(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        CardInstance targetUnit,
        string keyword
    )
    {
        if (
            targetUnit.ControllerPlayerIndex != player.PlayerIndex
            || targetUnit.InstanceId == card.InstanceId
        )
        {
            return false;
        }

        return string.Equals(keyword, "Vision", StringComparison.OrdinalIgnoreCase);
    }

    public override void OnUnitPlay(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        string actionId
    )
    {
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
                ["template"] = card.EffectTemplateId,
                ["revealedCardId"] = topCard.InstanceId.ToString(),
                ["revealed"] = topCard.Name,
            },
            Options =
            [
                new PendingChoiceOption
                {
                    ActionId = $"{runtime.ActionPrefix}choose-gemcraft-seer-keep-{topCard.InstanceId}",
                    Description = $"Gemcraft Seer: keep {topCard.Name} on top of your Main Deck",
                    Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["choice"] = "keep",
                    },
                },
                new PendingChoiceOption
                {
                    ActionId = $"{runtime.ActionPrefix}choose-gemcraft-seer-recycle-{topCard.InstanceId}",
                    Description = $"Gemcraft Seer: recycle {topCard.Name} to the bottom of your Main Deck",
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
                    : "named.gemcraft-seer",
                ["revealed"] = revealedCard.Name,
                ["kept"] = (!shouldRecycle).ToString().ToLowerInvariant(),
                ["recycled"] = shouldRecycle.ToString().ToLowerInvariant(),
            }
        );
    }
}

