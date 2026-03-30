using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class JeweledColossusEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "jeweled-colossus";
    public override string TemplateId => "named.jeweled-colossus";

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
            Kind = GemcraftSeerEffect.PendingChoiceKind,
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
                    ActionId = $"{runtime.ActionPrefix}choose-jeweled-colossus-keep-{topCard.InstanceId}",
                    Description = $"Vision: keep {topCard.Name} on top of your Main Deck",
                    Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["choice"] = "keep",
                    },
                },
                new PendingChoiceOption
                {
                    ActionId =
                        $"{runtime.ActionPrefix}choose-jeweled-colossus-recycle-{topCard.InstanceId}",
                    Description = $"Vision: recycle {topCard.Name} to the bottom of your Main Deck",
                    Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["choice"] = "recycle",
                    },
                },
            ],
        };
    }
}
