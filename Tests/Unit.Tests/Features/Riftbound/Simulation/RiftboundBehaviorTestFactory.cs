using Application.Features.Riftbound.Simulation.Definitions;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Unit.Tests.Features.Riftbound.Simulation;

internal static class RiftboundBehaviorTestFactory
{
    public static CardInstance BuildRuneInstance(
        long cardId,
        string runeName,
        string domain,
        int ownerPlayer
    )
    {
        var runeCard = new RiftboundCard
        {
            Id = cardId,
            Name = runeName,
            Type = "Rune",
            Color = [domain],
        };
        return BuildCardInstance(runeCard, ownerPlayer, ownerPlayer);
    }

    public static CardInstance BuildCardInstance(
        RiftboundCard card,
        int ownerPlayer,
        int controllerPlayer
    )
    {
        var template = RiftboundEffectTemplateResolver.Resolve(card);
        var keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (card.GameplayKeywords is not null)
        {
            foreach (var keyword in card.GameplayKeywords.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                keywords.Add(keyword.Trim());
            }
        }

        if (card.Tags is not null)
        {
            foreach (var tag in card.Tags.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                keywords.Add(tag.Trim());
            }
        }

        foreach (var keyword in template.Keywords)
        {
            keywords.Add(keyword);
        }

        return new CardInstance
        {
            InstanceId = Guid.NewGuid(),
            CardId = card.Id,
            Name = card.Name,
            Type = card.Type ?? "Card",
            OwnerPlayerIndex = ownerPlayer,
            ControllerPlayerIndex = controllerPlayer,
            Cost = card.Cost,
            Power = card.Power,
            ColorDomains = card.Color?.ToList() ?? [],
            Might = card.Might,
            Keywords = keywords.ToList(),
            EffectTemplateId = template.TemplateId,
            EffectData = template.Data.ToDictionary(
                x => x.Key,
                x => x.Value,
                StringComparer.OrdinalIgnoreCase
            ),
        };
    }

    public static CardInstance BuildUnit(
        int ownerPlayer,
        int controllerPlayer,
        string name,
        int might,
        bool isToken = false
    )
    {
        return new CardInstance
        {
            InstanceId = Guid.NewGuid(),
            CardId = Random.Shared.NextInt64(10_000, 99_999),
            Name = name,
            Type = "Unit",
            OwnerPlayerIndex = ownerPlayer,
            ControllerPlayerIndex = controllerPlayer,
            Cost = 1,
            Might = might,
            Keywords = [],
            IsToken = isToken,
        };
    }
}

