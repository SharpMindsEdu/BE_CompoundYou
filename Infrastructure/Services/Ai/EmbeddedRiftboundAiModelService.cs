using System.Text;
using System.Text.Json;
using Domain.Services.Ai;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services.Ai;

public sealed class EmbeddedRiftboundAiModelService
    : IRiftboundAiModelService, IRiftboundTrainingDataStore, IRiftboundAiOnlineTrainer
{
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
    private int _pendingUpdates;
    private DateTimeOffset _lastPersistUtc = DateTimeOffset.MinValue;

    public EmbeddedRiftboundAiModelService(
        IOptions<RiftboundAiModelOptions> options,
        ILogger<EmbeddedRiftboundAiModelService> logger
    )
    {
        _options = options.Value;
        _logger = logger;
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

        DeckProfile? profile;
        lock (_gate)
        {
            _deckProfilesByLegend.TryGetValue(pool.LegendId, out profile);
        }

        if (profile is null || profile.TotalSamples < _options.MinSamplesForDeckBuild)
        {
            return Task.FromResult<RiftboundDeckBuildProposal?>(null);
        }

        var championId = SelectCardId(pool.ChampionIds, profile.ChampionStats);
        var sharedMainSideboardCopies = new Dictionary<long, int>();
        var mainDeck = FillDeckWithCap(
            pool.MainDeckCardIds,
            request.MainDeckCardCount,
            cap: 3,
            profile.MainDeckStats,
            sharedMainSideboardCopies
        );
        var sideboard = FillDeckWithCap(
            pool.MainDeckCardIds,
            request.SideboardCardCount,
            cap: 3,
            profile.SideboardStats,
            sharedMainSideboardCopies
        );
        var runeDeck = FillDeckWithCap(
            pool.RuneCardIds,
            request.RuneDeckCardCount,
            cap: request.RuneDeckCardCount,
            profile.RuneDeckStats,
            null
        );
        var battlefields = SelectBattlefields(
            pool.BattlefieldCardIds,
            request.BattlefieldCardCount,
            profile.BattlefieldStats
        );

        if (mainDeck is null || sideboard is null || runeDeck is null || battlefields is null)
        {
            return Task.FromResult<RiftboundDeckBuildProposal?>(null);
        }

        return Task.FromResult<RiftboundDeckBuildProposal?>(
            new RiftboundDeckBuildProposal(
                pool.LegendId,
                championId,
                mainDeck.Select(x => new RiftboundDeckCardSelection(x.Key, x.Value)).ToList(),
                sideboard.Select(x => new RiftboundDeckCardSelection(x.Key, x.Value)).ToList(),
                runeDeck.Select(x => new RiftboundDeckCardSelection(x.Key, x.Value)).ToList(),
                battlefields
            )
        );
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

        lock (_gate)
        {
            foreach (var decision in episode.Decisions)
            {
                if (string.IsNullOrWhiteSpace(decision.SelectedActionId))
                {
                    continue;
                }

                var reward = episode.WinnerPlayerIndex switch
                {
                    null => 0d,
                    var winner when winner == decision.PlayerIndex => 1d,
                    _ => -1d,
                };

                var stateKey = BuildStateKey(decision.Decision);
                if (!_actionStatsByState.TryGetValue(stateKey, out var perAction))
                {
                    perAction = new Dictionary<string, ActionStat>(StringComparer.Ordinal);
                    _actionStatsByState[stateKey] = perAction;
                }

                UpdateActionStat(perAction, decision.SelectedActionId, reward);

                var actionType = decision
                    .Decision.LegalActions.FirstOrDefault(x =>
                        string.Equals(
                            x.ActionId,
                            decision.SelectedActionId,
                            StringComparison.Ordinal
                        )
                    )
                    ?.ActionType;
                if (!string.IsNullOrWhiteSpace(actionType))
                {
                    UpdateActionStat(_actionTypeStats, actionType, reward);
                }

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
                        PolicyId: "embedded-model",
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

                    actionScore += HeuristicActionBias(action);
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
        IReadOnlyCollection<long> candidates,
        IReadOnlyDictionary<long, ActionStat> stats
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
            .Select(cardId => (CardId: cardId, Score: ScoreCard(cardId, stats)))
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.CardId)
            .First()
            .CardId;
    }

    private Dictionary<long, int>? FillDeckWithCap(
        IReadOnlyCollection<long> poolIds,
        int totalCount,
        int cap,
        IReadOnlyDictionary<long, ActionStat> stats,
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

            var selected = SelectCardId(available, stats);
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
        IReadOnlyCollection<long> poolIds,
        int count,
        IReadOnlyDictionary<long, ActionStat> stats
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
            .Select(cardId => (CardId: cardId, Score: ScoreCard(cardId, stats)))
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.CardId)
            .Take(count)
            .Select(x => x.CardId)
            .ToList();

        return ranked.Count == count ? ranked : null;
    }

    private static double ScoreCard(long cardId, IReadOnlyDictionary<long, ActionStat> stats)
    {
        if (!stats.TryGetValue(cardId, out var stat) || stat.Count == 0)
        {
            return 0.10d;
        }

        return (stat.RewardSum / stat.Count) + Math.Log10(stat.Count + 1) * 0.05d;
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
                foreach (var state in snapshot.ActionStatsByState)
                {
                    _actionStatsByState[state.Key] = state
                        .Value.ToDictionary(
                            kvp => kvp.Key,
                            kvp => new ActionStat { Count = kvp.Value.Count, RewardSum = kvp.Value.RewardSum },
                            StringComparer.Ordinal
                        );
                }

                _actionTypeStats.Clear();
                foreach (var type in snapshot.ActionTypeStats)
                {
                    _actionTypeStats[type.Key] = new ActionStat
                    {
                        Count = type.Value.Count,
                        RewardSum = type.Value.RewardSum,
                    };
                }

                _deckProfilesByLegend.Clear();
                foreach (var profileEntry in snapshot.DeckProfilesByLegend)
                {
                    _deckProfilesByLegend[profileEntry.Key] = DeckProfile.FromSnapshot(profileEntry.Value);
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

        return new ModelSnapshot(actionByState, actionTypes, deckProfiles, updatedAtUtc);
    }

    private static string ResolvePath(string path)
    {
        return Path.IsPathRooted(path)
            ? path
            : Path.GetFullPath(path, Directory.GetCurrentDirectory());
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
                    Count = x.Value.Count,
                    RewardSum = x.Value.RewardSum,
                };
            }

            foreach (var x in snapshot.MainDeckStats)
            {
                profile.MainDeckStats[x.Key] = new ActionStat
                {
                    Count = x.Value.Count,
                    RewardSum = x.Value.RewardSum,
                };
            }

            foreach (var x in snapshot.SideboardStats)
            {
                profile.SideboardStats[x.Key] = new ActionStat
                {
                    Count = x.Value.Count,
                    RewardSum = x.Value.RewardSum,
                };
            }

            foreach (var x in snapshot.RuneDeckStats)
            {
                profile.RuneDeckStats[x.Key] = new ActionStat
                {
                    Count = x.Value.Count,
                    RewardSum = x.Value.RewardSum,
                };
            }

            foreach (var x in snapshot.BattlefieldStats)
            {
                profile.BattlefieldStats[x.Key] = new ActionStat
                {
                    Count = x.Value.Count,
                    RewardSum = x.Value.RewardSum,
                };
            }

            return profile;
        }
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
        DateTimeOffset UpdatedAtUtc
    );
}
