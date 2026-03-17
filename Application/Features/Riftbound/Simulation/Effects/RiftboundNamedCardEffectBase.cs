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

    public virtual bool TryAddUnitPlayLegalActions(
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

    public virtual void OnConquer(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        BattlefieldState battlefield
    ) { }

    public virtual void OnBattlefieldBeginning(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        BattlefieldState battlefield
    ) { }

    public virtual void OnBattlefieldShowdownStart(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        CardInstance card,
        BattlefieldState battlefield,
        int attackerPlayerIndex,
        int defenderPlayerIndex
    ) { }

    public virtual void OnUnitMoveFromBattlefield(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        BattlefieldState battlefield,
        CardInstance movedUnit
    ) { }

    public virtual int GetBattlefieldUnitMightModifier(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        BattlefieldState battlefield,
        CardInstance unit
    )
    {
        return 0;
    }

    public virtual int GetVictoryScoreModifier(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        CardInstance card,
        BattlefieldState battlefield
    )
    {
        return 0;
    }

    public virtual void OnUnitMove(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card
    ) { }

    public virtual void OnShowdownStart(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        BattlefieldState battlefield,
        bool isAttacker,
        bool isDefender
    ) { }

    public virtual void OnFriendlyUnitDeath(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        CardInstance deadFriendlyUnit
    ) { }

    public virtual void OnGearAttached(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        CardInstance attachedGear,
        CardInstance targetUnit
    ) { }

    public virtual void OnHoldScore(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        BattlefieldState battlefield
    ) { }

    public virtual void OnEndTurn(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card
    ) { }

    public virtual void OnDiscardFromHand(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        CardInstance? sourceCard,
        string reason
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
