using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public interface IRiftboundEffectRuntime
{
    string ActionPrefix { get; }
    string MultiTargetUnitsMarker { get; }
    string RepeatActionSuffix { get; }

    int ReadMagnitude(CardInstance card, int fallback);
    int ReadIntEffectData(CardInstance card, string key, int fallback);
    string? ReadEffectDataString(CardInstance card, string key);
    bool IsRepeatRequested(string actionId);

    IReadOnlyCollection<RiftboundTargetSelection> EnumerateSameLocationEnemyTargetSelections(
        GameSession session,
        int actingPlayerIndex,
        int maxTargets
    );

    (
        IReadOnlyCollection<CardInstance> Targets,
        string LocationKey
    ) ResolveSelectedSameLocationEnemyTargets(
        GameSession session,
        int actingPlayerIndex,
        string actionId,
        int maxTargets
    );

    bool TryPayRepeatCost(GameSession session, PlayerState player, CardInstance card);
    RiftboundRevealResolution ResolveTopDeckRevealEffects(
        GameSession session,
        PlayerState player,
        CardInstance revealedCard,
        CardInstance sourceCard
    );
    bool TryPlayCardFromReveal(
        GameSession session,
        PlayerState player,
        CardInstance revealedCard,
        CardInstance sourceCard,
        int energyCostReduction = 0,
        bool payAccelerateAdditionalCost = false,
        int? preferredBattlefieldIndex = null
    );

    void AddEffectContext(
        GameSession session,
        string source,
        int controllerPlayerIndex,
        string timing,
        IDictionary<string, string>? metadata = null
    );

    void DiscardFromHand(
        GameSession session,
        PlayerState player,
        CardInstance card,
        string reason,
        CardInstance? sourceCard = null
    );

    void DrawCards(PlayerState player, int count);
    void AddPower(PlayerState player, string domain, int amount);
}

public sealed record RiftboundTargetSelection(string LocationKey, IReadOnlyList<CardInstance> Targets);
public sealed record RiftboundRevealResolution(bool PlayedCard, int AddedEnergy);
