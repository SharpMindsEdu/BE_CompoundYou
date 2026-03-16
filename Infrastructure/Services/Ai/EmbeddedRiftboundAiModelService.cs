using System.Text;
using System.Text.Json;
using Domain.Services.Ai;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services.Ai;

public sealed class EmbeddedRiftboundAiModelService
    : IRiftboundAiModelService, IRiftboundTrainingDataStore, IRiftboundAiOnlineTrainer
{
    private const string ModelPolicyId = "embedded-model";
    private const int ActionFeatureCount = 24;
    private const int DeckFeatureCount = 16;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };
    private static readonly SemaphoreSlim FileWriteLock = new(1, 1);

    private readonly object _gate = new();
    private readonly RiftboundAiModelOptions _options;
    private readonly ILogger<EmbeddedRiftboundAiModelService> _logger;
    private readonly Dictionary<string, Dictionary<string, ActionStat>> _actionStatsByState = new(
        StringComparer.Ordinal
    );
    private readonly Dictionary<string, ActionStat> _actionTypeStats = new(
        StringComparer.OrdinalIgnoreCase
    );
    private readonly Dictionary<long, DeckProfile> _deckProfilesByLegend = [];
    private readonly OnlineDenseNetwork _actionNetwork;
    private readonly OnlineDenseNetwork _deckNetwork;
    private int _actionTrainingSamples;
    private int _deckTrainingSamples;
    private int _pendingUpdates;
    private DateTimeOffset _lastPersistUtc = DateTimeOffset.MinValue;

    public EmbeddedRiftboundAiModelService(
        IOptions<RiftboundAiModelOptions> options,
        ILogger<EmbeddedRiftboundAiModelService> logger
    )
    {
        _options = options.Value;
        _logger = logger;
        var seed = _options.NetworkSeed;
        _actionNetwork = new OnlineDenseNetwork(
            ActionFeatureCount,
            Math.Clamp(_options.ActionNetworkHiddenSize, 8, 256),
            _options.ActionLearningRate,
            _options.L2Regularization,
            seed ^ 0x41A7
        );
        _deckNetwork = new OnlineDenseNetwork(
            DeckFeatureCount,
            Math.Clamp(_options.DeckNetworkHiddenSize, 8, 256),
            _options.DeckLearningRate,
            _options.L2Regularization,
            seed ^ 0xD38E
        );
        TryLoadSnapshot();
    }

    public Task<string?> SelectActionIdAsync(
        RiftboundActionDecisionRequest request,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(SelectActionIdInternal(request));
    }

    public Task<string?> SelectReactionIdAsync(
        RiftboundActionDecisionRequest request,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(SelectActionIdInternal(request));
    }

    public Task<RiftboundDeckBuildProposal?> BuildDeckAsync(
        RiftboundDeckBuildRequest request,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_options.Enabled)
        {
            return Task.FromResult<RiftboundDeckBuildProposal?>(null);
        }

        var pool = request.Pool;
        if (
            pool.ChampionIds.Count == 0
            || pool.MainDeckCardIds.Count == 0
            || pool.RuneCardIds.Count == 0
            || pool.BattlefieldCardIds.Count == 0
        )
        {
            return Task.FromResult<RiftboundDeckBuildProposal?>(null);
        }

        RiftboundDeckBuildProposal? proposal = null;
        lock (_gate)
        {
            if (
                _deckProfilesByLegend.TryGetValue(pool.LegendId, out var profile)
                && profile.TotalSamples >= _options.MinSamplesForDeckBuild
            )
            {
                var sharedMainSideboardCopies = new Dictionary<long, int>();
                var championId = SelectCardId(
                    pool.LegendId,
                    pool.ChampionIds,
                    profile.ChampionStats,
                    profile.TotalSamples,
                    DeckCardRole.Champion,
                    pool.ChampionIds.Count,
                    1
                );
                var mainDeck = FillDeckWithCap(
                    pool.LegendId,
                    pool.MainDeckCardIds,
                    request.MainDeckCardCount,
                    cap: 3,
                    profile.MainDeckStats,
                    profile.TotalSamples,
                    DeckCardRole.MainDeck,
                    sharedMainSideboardCopies
                );
                var sideboard = FillDeckWithCap(
                    pool.LegendId,
                    pool.MainDeckCardIds,
                    request.SideboardCardCount,
                    cap: 3,
                    profile.SideboardStats,
                    profile.TotalSamples,
                    DeckCardRole.Sideboard,
                    sharedMainSideboardCopies
                );
                var runeDeck = FillDeckWithCap(
                    pool.LegendId,
                    pool.RuneCardIds,
                    request.RuneDeckCardCount,
                    cap: request.RuneDeckCardCount,
                    profile.RuneDeckStats,
                    profile.TotalSamples,
                    DeckCardRole.RuneDeck,
                    null
                );
                var battlefields = SelectBattlefields(
                    pool.LegendId,
                    pool.BattlefieldCardIds,
                    request.BattlefieldCardCount,
                    profile.BattlefieldStats,
                    profile.TotalSamples
                );

                if (
                    championId > 0
                    && mainDeck is not null
                    && sideboard is not null
                    && runeDeck is not null
                    && battlefields is not null
                )
                {
                    proposal = new RiftboundDeckBuildProposal(
                        pool.LegendId,
                        championId,
                        mainDeck.Select(x => new RiftboundDeckCardSelection(x.Key, x.Value)).ToList(),
                        sideboard.Select(x => new RiftboundDeckCardSelection(x.Key, x.Value)).ToList(),
                        runeDeck.Select(x => new RiftboundDeckCardSelection(x.Key, x.Value)).ToList(),
                        battlefields
                    );
                }
            }
        }

        return Task.FromResult(proposal);
    }

    public async Task TrainFromEpisodeAsync(
        RiftboundAiEpisode episode,
        CancellationToken cancellationToken = default
    )
    {
        if (!_options.TrainingEnabled || episode.Decisions.Count == 0)
        {
            return;
        }

        var counterfactualScale = Math.Clamp(_options.CounterfactualPenaltyScale, 0d, 1d);
        lock (_gate)
        {
            foreach (var decisionEvent in episode.Decisions)
            {
                var selectedActionId = decisionEvent.SelectedActionId?.Trim();
                if (string.IsNullOrWhiteSpace(selectedActionId))
                {
                    continue;
                }

                var reward = ResolveReward(episode.WinnerPlayerIndex, decisionEvent.PlayerIndex);
                var decision = decisionEvent.Decision;
                var stateKey = BuildStateKey(decision);
                if (!_actionStatsByState.TryGetValue(stateKey, out var perAction))
                {
                    perAction = new Dictionary<string, ActionStat>(StringComparer.Ordinal);
                    _actionStatsByState[stateKey] = perAction;
                }

                UpdateActionStat(perAction, selectedActionId, reward);

                var selectedAction = decision.LegalActions.FirstOrDefault(x =>
                    string.Equals(x.ActionId, selectedActionId, StringComparison.Ordinal)
                );
                if (!string.IsNullOrWhiteSpace(selectedAction?.ActionType))
                {
                    UpdateActionStat(_actionTypeStats, selectedAction.ActionType, reward);
                }

                if (_options.UseNeuralNetwork && selectedAction is not null)
                {
                    var selectedFeatures = BuildActionFeatures(
                        decision,
                        selectedAction,
                        decision.LegalActions.Count
                    );
                    _actionNetwork.Train(selectedFeatures, reward);

                    if (counterfactualScale > 0d && decision.LegalActions.Count > 1)
                    {
                        var counterfactualTarget = -reward * counterfactualScale;
                        foreach (var legalAction in decision.LegalActions)
                        {
                            if (
                                string.Equals(
                                    legalAction.ActionId,
                                    selectedActionId,
                                    StringComparison.Ordinal
                                )
                            )
                            {
                                continue;
                            }

                            var counterfactualFeatures = BuildActionFeatures(
                                decision,
                                legalAction,
                                decision.LegalActions.Count
                            );
                            _actionNetwork.Train(
                                counterfactualFeatures,
                                counterfactualTarget,
                                sampleWeight: 0.35d
                            );
                        }
                    }
                }

                _actionTrainingSamples++;
                _pendingUpdates++;
            }
        }

        if (_options.CaptureTrainingData)
        {
            foreach (var decision in episode.Decisions)
            {
                await RecordActionSampleAsync(
                    new RiftboundActionTrainingSample(
                        DateTimeOffset.UtcNow,
                        PolicyId: ModelPolicyId,
                        SelectionSource: episode.Source,
                        SelectedActionId: decision.SelectedActionId,
                        Decision: decision.Decision
                    ),
                    cancellationToken
                );
            }
        }

        await PersistSnapshotIfNeededAsync(cancellationToken);
    }

    public async Task TrainDeckOutcomeAsync(
        RiftboundDeckTrainingOutcome outcome,
        CancellationToken cancellationToken = default
    )
    {
        if (!_options.TrainingEnabled)
        {
            return;
        }

        var reward = outcome.IsDraw ? 0d : (outcome.IsWinner ? 1d : -1d);
        lock (_gate)
        {
            if (!_deckProfilesByLegend.TryGetValue(outcome.LegendId, out var profile))
            {
                profile = new DeckProfile();
                _deckProfilesByLegend[outcome.LegendId] = profile;
            }

            profile.TotalSamples++;
            UpdateCardStat(profile.ChampionStats, outcome.ChampionId, reward, 1);
            foreach (var card in outcome.MainDeck)
            {
                UpdateCardStat(profile.MainDeckStats, card.CardId, reward, Math.Max(1, card.Quantity));
            }

            foreach (var card in outcome.Sideboard)
            {
                UpdateCardStat(
                    profile.SideboardStats,
                    card.CardId,
                    reward,
                    Math.Max(1, card.Quantity)
                );
            }

            foreach (var card in outcome.RuneDeck)
            {
                UpdateCardStat(profile.RuneDeckStats, card.CardId, reward, Math.Max(1, card.Quantity));
            }

            foreach (var battlefieldId in outcome.BattlefieldIds.Distinct())
            {
                UpdateCardStat(profile.BattlefieldStats, battlefieldId, reward, 1);
            }

            if (_options.UseNeuralNetwork)
            {
                TrainDeckNetworkNoLock(outcome, profile, reward);
            }

            _deckTrainingSamples++;
            _pendingUpdates++;
        }

        await PersistSnapshotIfNeededAsync(cancellationToken);
    }

    public Task RecordActionSampleAsync(
        RiftboundActionTrainingSample sample,
        CancellationToken cancellationToken = default
    )
    {
        return AppendJsonLineAsync("action-decisions.jsonl", sample, cancellationToken);
    }

    public Task RecordDeckBuildSampleAsync(
        RiftboundDeckBuildTrainingSample sample,
        CancellationToken cancellationToken = default
    )
    {
        return AppendJsonLineAsync("deck-builds.jsonl", sample, cancellationToken);
    }

    private string? SelectActionIdInternal(RiftboundActionDecisionRequest request)
    {
        var legalActions = request
            .LegalActions.Where(x => !string.IsNullOrWhiteSpace(x.ActionId))
            .GroupBy(x => x.ActionId, StringComparer.Ordinal)
            .Select(g => g.First())
            .ToList();

        if (legalActions.Count == 0)
        {
            return null;
        }

        if (!_options.Enabled)
        {
            return ChooseHeuristicLike(legalActions);
        }

        if (_options.ExplorationRate > 0d && Random.Shared.NextDouble() < _options.ExplorationRate)
        {
            return legalActions[Random.Shared.Next(legalActions.Count)].ActionId;
        }

        var stateKey = BuildStateKey(request);
        lock (_gate)
        {
            _actionStatsByState.TryGetValue(stateKey, out var perAction);
            var useActionNetwork = _options.UseNeuralNetwork
                && _actionTrainingSamples >= Math.Max(1, _options.MinActionSamplesForNeuralInference);
            var neuralWeight = useActionNetwork ? Math.Clamp(_options.NeuralScoreWeight, 0d, 2d) : 0d;
            var heuristicWeight = useActionNetwork ? 0.55d : 1d;

            var ranked = legalActions
                .Select(action =>
                {
                    var actionScore = 0d;
                    if (
                        perAction is not null
                        && perAction.TryGetValue(action.ActionId, out var actionStat)
                        && actionStat.Count > 0
                    )
                    {
                        actionScore += actionStat.RewardSum / actionStat.Count;
                        actionScore += Math.Log10(actionStat.Count + 1) * 0.08d;
                    }

                    if (
                        _actionTypeStats.TryGetValue(action.ActionType, out var typeStat)
                        && typeStat.Count > 0
                    )
                    {
                        actionScore += (typeStat.RewardSum / typeStat.Count) * 0.4d;
                    }

                    if (neuralWeight > 0d)
                    {
                        var features = BuildActionFeatures(request, action, legalActions.Count);
                        var neuralScore = _actionNetwork.Predict(features);
                        actionScore += neuralScore * neuralWeight;
                    }

                    actionScore += HeuristicActionBias(action) * heuristicWeight;
                    return (action.ActionId, actionScore);
                })
                .OrderByDescending(x => x.actionScore)
                .ThenBy(x => x.ActionId, StringComparer.Ordinal)
                .ToList();

            return ranked[0].ActionId;
        }
    }

    private static string ChooseHeuristicLike(IReadOnlyCollection<RiftboundActionCandidate> legalActions)
    {
        return legalActions
            .OrderByDescending(HeuristicActionBias)
            .ThenBy(x => x.ActionId, StringComparer.Ordinal)
            .Select(x => x.ActionId)
            .First();
    }

    private static double HeuristicActionBias(RiftboundActionCandidate action)
    {
        return action.ActionType switch
        {
            "PlayCard" when action.ActionId.Contains("-to-bf-", StringComparison.Ordinal) => 0.90d,
            "PlayCard" => 0.80d,
            "ActivateRune" => 0.70d,
            "StandardMove" when action.ActionId.Contains("-to-bf-", StringComparison.Ordinal) => 0.60d,
            "StandardMove" => 0.50d,
            "ResolveCombat" => 0.40d,
            "PassFocus" => 0.30d,
            "EndTurn" => 0.00d,
            _ => 0.10d,
        };
    }

    private static double[] BuildActionFeatures(
        RiftboundActionDecisionRequest request,
        RiftboundActionCandidate action,
        int legalActionCount
    )
    {
        var features = new double[ActionFeatureCount];
        features[0] = 1d;
        features[1] = NormalizeSigned(request.MyScore - request.OpponentScore, 12d);
        features[2] = NormalizeSigned(request.MyScore, 20d);
        features[3] = NormalizeSigned(request.OpponentScore, 20d);
        features[4] = Normalize(request.MyHandCount, 12d);
        features[5] = Normalize(request.MyRuneEnergy, 10d);
        features[6] = Normalize(request.MyBaseUnits, 8d);
        features[7] = Normalize(request.ControlledBattlefields.Count, 5d);
        features[8] = Normalize(request.TurnNumber, 20d);
        features[9] = Normalize(legalActionCount, 20d);
        features[10] = request.DecisionKind == RiftboundDecisionKind.ActionSelection ? 1d : 0d;
        features[11] = request.DecisionKind == RiftboundDecisionKind.ReactionSelection ? 1d : 0d;
        features[12] = string.IsNullOrWhiteSpace(request.LastOpponentActionId) ? 0d : 1d;
        features[13] = Math.Clamp(HeuristicActionBias(action), -1d, 1d);
        features[14] = action.ActionId.Contains("-to-bf-", StringComparison.Ordinal) ? 1d : 0d;
        features[15] = Normalize(action.Description?.Length ?? 0, 160d);
        features[16] = Normalize(action.ActionId.Length, 120d);
        features[17] = HashToSignedUnit(request.Phase);
        features[18] = HashToSignedUnit(request.State);
        features[19] = HashToSignedUnit(action.ActionType);
        features[20] = HashToSignedUnit(ShortToken(action.ActionId));
        features[21] = HashToSignedUnit(request.LastOpponentActionId);
        features[22] = HashToSignedUnit($"{request.Phase}:{action.ActionType}");
        features[23] = HashToSignedUnit($"{request.State}:{ShortToken(action.ActionId)}");
        return features;
    }

    private static string BuildStateKey(RiftboundActionDecisionRequest request)
    {
        var scoreDeltaBucket = Math.Clamp((request.MyScore - request.OpponentScore + 12) / 3, 0, 8);
        var handBucket = Math.Clamp(request.MyHandCount / 2, 0, 8);
        var energyBucket = Math.Clamp(request.MyRuneEnergy / 2, 0, 8);
        var baseBucket = Math.Clamp(request.MyBaseUnits, 0, 8);
        var battlefields = Math.Clamp(request.ControlledBattlefields.Count, 0, 5);
        var legalTypes = request
            .LegalActions.Select(x => x.ActionType)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase);

        return string.Join(
            '|',
            request.DecisionKind,
            request.Phase,
            request.State,
            $"score:{scoreDeltaBucket}",
            $"hand:{handBucket}",
            $"energy:{energyBucket}",
            $"base:{baseBucket}",
            $"bf:{battlefields}",
            $"types:{string.Join(",", legalTypes)}"
        );
    }

    private static void UpdateActionStat(
        IDictionary<string, ActionStat> stats,
        string key,
        double reward
    )
    {
        if (!stats.TryGetValue(key, out var stat))
        {
            stat = new ActionStat();
            stats[key] = stat;
        }

        stat.Count++;
        stat.RewardSum += reward;
    }

    private static void UpdateCardStat(
        IDictionary<long, ActionStat> stats,
        long cardId,
        double reward,
        int quantity
    )
    {
        if (cardId <= 0 || quantity <= 0)
        {
            return;
        }

        if (!stats.TryGetValue(cardId, out var stat))
        {
            stat = new ActionStat();
            stats[cardId] = stat;
        }

        stat.Count += quantity;
        stat.RewardSum += reward * quantity;
    }

    private long SelectCardId(
        long legendId,
        IReadOnlyCollection<long> candidates,
        IReadOnlyDictionary<long, ActionStat> stats,
        int legendSampleCount,
        DeckCardRole role,
        int poolSizeHint,
        int targetCountHint
    )
    {
        var unique = candidates.Where(x => x > 0).Distinct().ToList();
        if (unique.Count == 0)
        {
            return 0;
        }

        if (_options.ExplorationRate > 0d && Random.Shared.NextDouble() < _options.ExplorationRate)
        {
            return unique[Random.Shared.Next(unique.Count)];
        }

        return unique
            .Select(cardId =>
                (
                    CardId: cardId,
                    Score: ScoreCard(
                        legendId,
                        cardId,
                        role,
                        stats,
                        legendSampleCount,
                        poolSizeHint,
                        targetCountHint,
                        quantityHint: 1
                    )
                )
            )
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.CardId)
            .First()
            .CardId;
    }

    private Dictionary<long, int>? FillDeckWithCap(
        long legendId,
        IReadOnlyCollection<long> poolIds,
        int totalCount,
        int cap,
        IReadOnlyDictionary<long, ActionStat> stats,
        int legendSampleCount,
        DeckCardRole role,
        Dictionary<long, int>? sharedCopies
    )
    {
        var result = new Dictionary<long, int>();
        var candidates = poolIds.Where(x => x > 0).Distinct().ToList();
        if (candidates.Count == 0 || totalCount <= 0)
        {
            return null;
        }

        var attempts = 0;
        var maxAttempts = totalCount * 256;
        while (result.Values.Sum() < totalCount && attempts < maxAttempts)
        {
            attempts++;
            var available = candidates
                .Where(cardId =>
                {
                    var local = result.GetValueOrDefault(cardId);
                    var shared = sharedCopies?.GetValueOrDefault(cardId) ?? 0;
                    return local < cap && shared < cap;
                })
                .ToList();
            if (available.Count == 0)
            {
                break;
            }

            var selected = SelectCardId(
                legendId,
                available,
                stats,
                legendSampleCount,
                role,
                candidates.Count,
                totalCount
            );
            if (selected <= 0)
            {
                return null;
            }

            result[selected] = result.GetValueOrDefault(selected) + 1;
            if (sharedCopies is not null)
            {
                sharedCopies[selected] = sharedCopies.GetValueOrDefault(selected) + 1;
            }
        }

        return result.Values.Sum() == totalCount ? result : null;
    }

    private IReadOnlyCollection<long>? SelectBattlefields(
        long legendId,
        IReadOnlyCollection<long> poolIds,
        int count,
        IReadOnlyDictionary<long, ActionStat> stats,
        int legendSampleCount
    )
    {
        if (count <= 0)
        {
            return [];
        }

        var unique = poolIds.Where(x => x > 0).Distinct().ToList();
        if (unique.Count < count)
        {
            return null;
        }

        var ranked = unique
            .Select(cardId =>
                (
                    CardId: cardId,
                    Score: ScoreCard(
                        legendId,
                        cardId,
                        DeckCardRole.Battlefield,
                        stats,
                        legendSampleCount,
                        unique.Count,
                        count,
                        quantityHint: 1
                    )
                )
            )
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.CardId)
            .Take(count)
            .Select(x => x.CardId)
            .ToList();

        return ranked.Count == count ? ranked : null;
    }

    private double ScoreCard(
        long legendId,
        long cardId,
        DeckCardRole role,
        IReadOnlyDictionary<long, ActionStat> stats,
        int legendSampleCount,
        int poolSizeHint,
        int targetCountHint,
        int quantityHint
    )
    {
        var statScore = 0.10d;
        if (stats.TryGetValue(cardId, out var stat) && stat.Count > 0)
        {
            statScore = (stat.RewardSum / stat.Count) + Math.Log10(stat.Count + 1) * 0.05d;
        }

        var useDeckNetwork = _options.UseNeuralNetwork
            && legendSampleCount >= Math.Max(1, _options.MinDeckSamplesForNeuralInference);
        if (!useDeckNetwork)
        {
            return statScore;
        }

        var features = BuildDeckCardFeatures(
            legendId,
            cardId,
            role,
            stats,
            legendSampleCount,
            poolSizeHint,
            targetCountHint,
            quantityHint
        );
        var neuralScore = _deckNetwork.Predict(features);
        var neuralWeight = Math.Clamp(_options.NeuralScoreWeight, 0d, 2d);
        var statWeight = Math.Max(0.25d, 1.1d - neuralWeight);
        return (statScore * statWeight) + (neuralScore * neuralWeight);
    }

    private static double[] BuildDeckCardFeatures(
        long legendId,
        long cardId,
        DeckCardRole role,
        IReadOnlyDictionary<long, ActionStat> roleStats,
        int legendSampleCount,
        int poolSizeHint,
        int targetCountHint,
        int quantityHint
    )
    {
        var features = new double[DeckFeatureCount];
        features[0] = 1d;
        features[1] = role == DeckCardRole.Champion ? 1d : 0d;
        features[2] = role == DeckCardRole.MainDeck ? 1d : 0d;
        features[3] = role == DeckCardRole.Sideboard ? 1d : 0d;
        features[4] = role == DeckCardRole.RuneDeck ? 1d : 0d;
        features[5] = role == DeckCardRole.Battlefield ? 1d : 0d;

        var (avgReward, sampleCount, priorScore) = BuildCardStatFeature(roleStats, cardId);
        features[6] = Math.Clamp(avgReward, -1d, 1d);
        features[7] = NormalizeSigned(Math.Log10(sampleCount + 1d), 3d);
        features[8] = NormalizeSigned(Math.Log10(legendSampleCount + 1d), 3d);
        features[9] = Normalize(quantityHint, 12d);
        features[10] = Normalize(poolSizeHint, 120d);
        features[11] = Normalize(targetCountHint, 64d);
        features[12] = HashToSignedUnit(legendId.ToString());
        features[13] = HashToSignedUnit(cardId.ToString());
        features[14] = HashToSignedUnit($"{(int)role}:{legendId}:{cardId}");
        features[15] = priorScore;
        return features;
    }

    private static (double AverageReward, int SampleCount, double PriorScore) BuildCardStatFeature(
        IReadOnlyDictionary<long, ActionStat> roleStats,
        long cardId
    )
    {
        if (!roleStats.TryGetValue(cardId, out var stat) || stat.Count <= 0)
        {
            return (0d, 0, 0.10d);
        }

        var averageReward = stat.RewardSum / stat.Count;
        var priorScore = Math.Clamp(averageReward + Math.Log10(stat.Count + 1) * 0.05d, -1d, 1d);
        return (averageReward, stat.Count, priorScore);
    }

    private void TrainDeckNetworkNoLock(
        RiftboundDeckTrainingOutcome outcome,
        DeckProfile profile,
        double reward
    )
    {
        var safeReward = Math.Clamp(reward, -1d, 1d);
        var legendSamples = Math.Max(1, profile.TotalSamples);

        TrainDeckCardNoLock(
            outcome.LegendId,
            outcome.ChampionId,
            DeckCardRole.Champion,
            profile.ChampionStats,
            legendSamples,
            poolSizeHint: Math.Max(1, outcome.MainDeck.Select(x => x.CardId).Distinct().Count()),
            targetCountHint: 1,
            quantityHint: 1,
            safeReward
        );

        var mainPool = Math.Max(1, outcome.MainDeck.Select(x => x.CardId).Distinct().Count());
        var mainCount = Math.Max(1, outcome.MainDeck.Sum(x => Math.Max(1, x.Quantity)));
        foreach (var card in outcome.MainDeck)
        {
            TrainDeckCardNoLock(
                outcome.LegendId,
                card.CardId,
                DeckCardRole.MainDeck,
                profile.MainDeckStats,
                legendSamples,
                mainPool,
                mainCount,
                Math.Max(1, card.Quantity),
                safeReward
            );
        }

        var sidePool = Math.Max(1, outcome.Sideboard.Select(x => x.CardId).Distinct().Count());
        var sideCount = Math.Max(1, outcome.Sideboard.Sum(x => Math.Max(1, x.Quantity)));
        foreach (var card in outcome.Sideboard)
        {
            TrainDeckCardNoLock(
                outcome.LegendId,
                card.CardId,
                DeckCardRole.Sideboard,
                profile.SideboardStats,
                legendSamples,
                sidePool,
                sideCount,
                Math.Max(1, card.Quantity),
                safeReward
            );
        }

        var runePool = Math.Max(1, outcome.RuneDeck.Select(x => x.CardId).Distinct().Count());
        var runeCount = Math.Max(1, outcome.RuneDeck.Sum(x => Math.Max(1, x.Quantity)));
        foreach (var card in outcome.RuneDeck)
        {
            TrainDeckCardNoLock(
                outcome.LegendId,
                card.CardId,
                DeckCardRole.RuneDeck,
                profile.RuneDeckStats,
                legendSamples,
                runePool,
                runeCount,
                Math.Max(1, card.Quantity),
                safeReward
            );
        }

        var battlefields = outcome.BattlefieldIds.Where(x => x > 0).Distinct().ToList();
        var battlefieldPool = Math.Max(1, battlefields.Count);
        foreach (var cardId in battlefields)
        {
            TrainDeckCardNoLock(
                outcome.LegendId,
                cardId,
                DeckCardRole.Battlefield,
                profile.BattlefieldStats,
                legendSamples,
                battlefieldPool,
                targetCountHint: battlefieldPool,
                quantityHint: 1,
                safeReward
            );
        }
    }

    private void TrainDeckCardNoLock(
        long legendId,
        long cardId,
        DeckCardRole role,
        IReadOnlyDictionary<long, ActionStat> roleStats,
        int legendSampleCount,
        int poolSizeHint,
        int targetCountHint,
        int quantityHint,
        double reward
    )
    {
        if (cardId <= 0 || quantityHint <= 0)
        {
            return;
        }

        var features = BuildDeckCardFeatures(
            legendId,
            cardId,
            role,
            roleStats,
            legendSampleCount,
            poolSizeHint,
            targetCountHint,
            quantityHint
        );
        var sampleWeight = Math.Clamp(quantityHint / 2d, 0.5d, 4d);
        _deckNetwork.Train(features, reward, sampleWeight);
    }

    private async Task AppendJsonLineAsync(
        string fileName,
        object payload,
        CancellationToken cancellationToken
    )
    {
        if (!_options.CaptureTrainingData || string.IsNullOrWhiteSpace(_options.TrainingDataDirectory))
        {
            return;
        }

        var directory = ResolvePath(_options.TrainingDataDirectory);
        var path = Path.Combine(directory, fileName);
        var line = JsonSerializer.Serialize(payload, JsonOptions) + Environment.NewLine;
        var lockTaken = false;

        try
        {
            Directory.CreateDirectory(directory);
            await FileWriteLock.WaitAsync(cancellationToken);
            lockTaken = true;
            await File.AppendAllTextAsync(path, line, Encoding.UTF8, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to append Riftbound training data to {Path}.", path);
        }
        finally
        {
            if (lockTaken)
            {
                FileWriteLock.Release();
            }
        }
    }

    private async Task PersistSnapshotIfNeededAsync(CancellationToken cancellationToken)
    {
        if (!_options.PersistModelToDisk || string.IsNullOrWhiteSpace(_options.ModelFilePath))
        {
            return;
        }

        ModelSnapshot? snapshot = null;
        var now = DateTimeOffset.UtcNow;
        lock (_gate)
        {
            if (_pendingUpdates == 0)
            {
                return;
            }

            var minIntervalSeconds = Math.Max(5, _options.AutosaveIntervalSeconds);
            var dueByTime = (now - _lastPersistUtc).TotalSeconds >= minIntervalSeconds;
            var dueByVolume = _pendingUpdates >= 50;
            if (!dueByTime && !dueByVolume)
            {
                return;
            }

            snapshot = CreateSnapshotNoLock(now);
            _pendingUpdates = 0;
            _lastPersistUtc = now;
        }

        var path = ResolvePath(_options.ModelFilePath);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        try
        {
            var json = JsonSerializer.Serialize(snapshot, JsonOptions);
            await File.WriteAllTextAsync(path, json, Encoding.UTF8, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to persist embedded Riftbound model snapshot to {Path}.",
                path
            );
        }
    }

    private void TryLoadSnapshot()
    {
        if (!_options.PersistModelToDisk || string.IsNullOrWhiteSpace(_options.ModelFilePath))
        {
            return;
        }

        var path = ResolvePath(_options.ModelFilePath);
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            var json = File.ReadAllText(path, Encoding.UTF8);
            var snapshot = JsonSerializer.Deserialize<ModelSnapshot>(json, JsonOptions);
            if (snapshot is null)
            {
                return;
            }

            lock (_gate)
            {
                _actionStatsByState.Clear();
                foreach (var state in snapshot.ActionStatsByState ?? [])
                {
                    if (state.Value is null)
                    {
                        continue;
                    }

                    _actionStatsByState[state.Key] = state
                        .Value.ToDictionary(
                            kvp => kvp.Key,
                            kvp => new ActionStat
                            {
                                Count = kvp.Value?.Count ?? 0,
                                RewardSum = kvp.Value?.RewardSum ?? 0d,
                            },
                            StringComparer.Ordinal
                        );
                }

                _actionTypeStats.Clear();
                foreach (var type in snapshot.ActionTypeStats ?? [])
                {
                    _actionTypeStats[type.Key] = new ActionStat
                    {
                        Count = type.Value?.Count ?? 0,
                        RewardSum = type.Value?.RewardSum ?? 0d,
                    };
                }

                _deckProfilesByLegend.Clear();
                foreach (var profileEntry in snapshot.DeckProfilesByLegend ?? [])
                {
                    if (profileEntry.Value is null)
                    {
                        continue;
                    }

                    _deckProfilesByLegend[profileEntry.Key] = DeckProfile.FromSnapshot(profileEntry.Value);
                }

                _actionTrainingSamples = Math.Max(snapshot.ActionTrainingSamples, 0);
                _deckTrainingSamples = Math.Max(snapshot.DeckTrainingSamples, 0);

                if (
                    snapshot.ActionNetwork is not null
                    && !_actionNetwork.TryLoadSnapshot(snapshot.ActionNetwork)
                )
                {
                    _logger.LogWarning(
                        "Stored action-network snapshot is incompatible with current model settings."
                    );
                }

                if (
                    snapshot.DeckNetwork is not null
                    && !_deckNetwork.TryLoadSnapshot(snapshot.DeckNetwork)
                )
                {
                    _logger.LogWarning(
                        "Stored deck-network snapshot is incompatible with current model settings."
                    );
                }

                _lastPersistUtc = snapshot.UpdatedAtUtc;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to load embedded Riftbound model snapshot from {Path}.",
                path
            );
        }
    }

    private ModelSnapshot CreateSnapshotNoLock(DateTimeOffset updatedAtUtc)
    {
        var actionByState = _actionStatsByState.ToDictionary(
            state => state.Key,
            state => state.Value.ToDictionary(
                action => action.Key,
                action => new StatSnapshot(action.Value.Count, action.Value.RewardSum),
                StringComparer.Ordinal
            ),
            StringComparer.Ordinal
        );

        var actionTypes = _actionTypeStats.ToDictionary(
            x => x.Key,
            x => new StatSnapshot(x.Value.Count, x.Value.RewardSum),
            StringComparer.OrdinalIgnoreCase
        );

        var deckProfiles = _deckProfilesByLegend.ToDictionary(
            x => x.Key,
            x => x.Value.ToSnapshot()
        );

        return new ModelSnapshot(
            actionByState,
            actionTypes,
            deckProfiles,
            updatedAtUtc,
            _actionTrainingSamples,
            _deckTrainingSamples,
            _actionNetwork.ToSnapshot(),
            _deckNetwork.ToSnapshot()
        );
    }

    private static string ResolvePath(string path)
    {
        return Path.IsPathRooted(path)
            ? path
            : Path.GetFullPath(path, Directory.GetCurrentDirectory());
    }

    private static double ResolveReward(int? winnerPlayerIndex, int decisionPlayerIndex)
    {
        return winnerPlayerIndex switch
        {
            null => 0d,
            var winner when winner == decisionPlayerIndex => 1d,
            _ => -1d,
        };
    }

    private static double Normalize(double value, double maxAbs)
    {
        var divider = Math.Max(0.000001d, maxAbs);
        return Math.Clamp(value / divider, 0d, 1d);
    }

    private static double NormalizeSigned(double value, double maxAbs)
    {
        var divider = Math.Max(0.000001d, maxAbs);
        return Math.Clamp(value / divider, -1d, 1d);
    }

    private static double HashToSignedUnit(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0d;
        }

        unchecked
        {
            ulong hash = 1469598103934665603UL;
            foreach (var ch in value)
            {
                hash ^= ch;
                hash *= 1099511628211UL;
            }

            return ((hash % 2_000_000UL) / 1_000_000d) - 1d;
        }
    }

    private static string ShortToken(string value)
    {
        return value.Length <= 40 ? value : value[..40];
    }

    private sealed class ActionStat
    {
        public int Count { get; set; }

        public double RewardSum { get; set; }
    }

    private sealed class DeckProfile
    {
        public int TotalSamples { get; set; }

        public Dictionary<long, ActionStat> ChampionStats { get; } = [];

        public Dictionary<long, ActionStat> MainDeckStats { get; } = [];

        public Dictionary<long, ActionStat> SideboardStats { get; } = [];

        public Dictionary<long, ActionStat> RuneDeckStats { get; } = [];

        public Dictionary<long, ActionStat> BattlefieldStats { get; } = [];

        public DeckProfileSnapshot ToSnapshot()
        {
            return new DeckProfileSnapshot(
                TotalSamples,
                ChampionStats.ToDictionary(x => x.Key, x => new StatSnapshot(x.Value.Count, x.Value.RewardSum)),
                MainDeckStats.ToDictionary(x => x.Key, x => new StatSnapshot(x.Value.Count, x.Value.RewardSum)),
                SideboardStats.ToDictionary(
                    x => x.Key,
                    x => new StatSnapshot(x.Value.Count, x.Value.RewardSum)
                ),
                RuneDeckStats.ToDictionary(
                    x => x.Key,
                    x => new StatSnapshot(x.Value.Count, x.Value.RewardSum)
                ),
                BattlefieldStats.ToDictionary(
                    x => x.Key,
                    x => new StatSnapshot(x.Value.Count, x.Value.RewardSum)
                )
            );
        }

        public static DeckProfile FromSnapshot(DeckProfileSnapshot snapshot)
        {
            var profile = new DeckProfile { TotalSamples = snapshot.TotalSamples };
            foreach (var x in snapshot.ChampionStats)
            {
                profile.ChampionStats[x.Key] = new ActionStat
                {
                    Count = x.Value?.Count ?? 0,
                    RewardSum = x.Value?.RewardSum ?? 0d,
                };
            }

            foreach (var x in snapshot.MainDeckStats)
            {
                profile.MainDeckStats[x.Key] = new ActionStat
                {
                    Count = x.Value?.Count ?? 0,
                    RewardSum = x.Value?.RewardSum ?? 0d,
                };
            }

            foreach (var x in snapshot.SideboardStats)
            {
                profile.SideboardStats[x.Key] = new ActionStat
                {
                    Count = x.Value?.Count ?? 0,
                    RewardSum = x.Value?.RewardSum ?? 0d,
                };
            }

            foreach (var x in snapshot.RuneDeckStats)
            {
                profile.RuneDeckStats[x.Key] = new ActionStat
                {
                    Count = x.Value?.Count ?? 0,
                    RewardSum = x.Value?.RewardSum ?? 0d,
                };
            }

            foreach (var x in snapshot.BattlefieldStats)
            {
                profile.BattlefieldStats[x.Key] = new ActionStat
                {
                    Count = x.Value?.Count ?? 0,
                    RewardSum = x.Value?.RewardSum ?? 0d,
                };
            }

            return profile;
        }
    }

    private enum DeckCardRole
    {
        Champion = 1,
        MainDeck = 2,
        Sideboard = 3,
        RuneDeck = 4,
        Battlefield = 5,
    }

    private sealed record StatSnapshot(int Count, double RewardSum);

    private sealed record DeckProfileSnapshot(
        int TotalSamples,
        Dictionary<long, StatSnapshot> ChampionStats,
        Dictionary<long, StatSnapshot> MainDeckStats,
        Dictionary<long, StatSnapshot> SideboardStats,
        Dictionary<long, StatSnapshot> RuneDeckStats,
        Dictionary<long, StatSnapshot> BattlefieldStats
    );

    private sealed record ModelSnapshot(
        Dictionary<string, Dictionary<string, StatSnapshot>> ActionStatsByState,
        Dictionary<string, StatSnapshot> ActionTypeStats,
        Dictionary<long, DeckProfileSnapshot> DeckProfilesByLegend,
        DateTimeOffset UpdatedAtUtc,
        int ActionTrainingSamples = 0,
        int DeckTrainingSamples = 0,
        OnlineDenseNetworkSnapshot? ActionNetwork = null,
        OnlineDenseNetworkSnapshot? DeckNetwork = null
    );
}
