using Application.Features.Riftbound.Simulation.Definitions;
using Domain.Entities.Riftbound;

namespace Application.Features.Riftbound.Simulation.Effects;

public static class RiftboundNamedCardEffectCatalog
{
    private static readonly IReadOnlyCollection<IRiftboundNamedCardEffect> Effects =
    [
        new BellowsBreathEffect(),
        new StackedDeckEffect(),
        new CalledShotEffect(),
        new DisciplineEffect(),
        new EnGardeEffect(),
        new SealOfDiscordEffect(),
        new NocturneHorrifyingEffect(),
        new UndertitanEffect(),
    ];

    private static readonly IReadOnlyDictionary<string, IRiftboundNamedCardEffect> EffectsByNameIdentifier = Effects.ToDictionary(
        effect => effect.NameIdentifier,
        effect => effect,
        StringComparer.OrdinalIgnoreCase
    );

    private static readonly IReadOnlyDictionary<string, IRiftboundNamedCardEffect> EffectsByTemplateId = Effects.ToDictionary(
        effect => effect.TemplateId,
        effect => effect,
        StringComparer.Ordinal
    );

    public static bool TryResolve(
        RiftboundCard card,
        string normalizedEffectText,
        IReadOnlySet<string> baseKeywords,
        out RiftboundResolvedEffectTemplate resolved
    )
    {
        var identifier = RiftboundCardNameIdentifier.FromName(card.Name);
        if (EffectsByNameIdentifier.TryGetValue(identifier, out var effect))
        {
            resolved = effect.ResolveTemplate(card, normalizedEffectText, baseKeywords);
            return true;
        }

        resolved = default!;
        return false;
    }

    public static bool TryGetByTemplateId(
        string templateId,
        out IRiftboundNamedCardEffect effect
    )
    {
        return EffectsByTemplateId.TryGetValue(templateId, out effect!);
    }
}
