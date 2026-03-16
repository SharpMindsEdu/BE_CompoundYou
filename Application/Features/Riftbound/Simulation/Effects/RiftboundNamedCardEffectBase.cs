using Application.Features.Riftbound.Simulation.Definitions;
using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public abstract class RiftboundNamedCardEffectBase : IRiftboundNamedCardEffect
{
    public abstract string NameIdentifier { get; }
    public abstract string TemplateId { get; }
    public virtual bool HasActivatedAbility => false;

    public virtual RiftboundResolvedEffectTemplate ResolveTemplate(
        RiftboundCard card,
        string normalizedEffectText,
        IReadOnlySet<string> baseKeywords
    )
    {
        return new RiftboundResolvedEffectTemplate(
            Supported: true,
            TemplateId: TemplateId,
            Keywords: BuildKeywords(card, normalizedEffectText, baseKeywords),
            Data: BuildData(card, normalizedEffectText)
        );
    }

    protected virtual IReadOnlyCollection<string> BuildKeywords(
        RiftboundCard card,
        string normalizedEffectText,
        IReadOnlySet<string> baseKeywords
    )
    {
        return baseKeywords.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
    }

    protected virtual IReadOnlyDictionary<string, string> BuildData(
        RiftboundCard card,
        string normalizedEffectText
    )
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public virtual bool TryAddLegalActions(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        List<RiftboundLegalAction> actions
    )
    {
        return false;
    }

    public virtual void OnSpellOrGearPlay(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        string actionId
    ) { }

    public virtual void OnUnitPlay(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        string actionId
    ) { }

    public virtual bool TryActivateAbility(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card
    )
    {
        return false;
    }
}
