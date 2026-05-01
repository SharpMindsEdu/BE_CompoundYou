using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Diagnostics;
using Application.Features.Trading.Automation;
using Application.Features.Trading.Live;
using Domain.Services.Trading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services.Trading;

public sealed class TradingAutomationBackgroundService : BackgroundService
{
    private const int FallbackMarketOpenHour = 9;
    private const int FallbackMarketOpenMinute = 30;
    private static readonly Regex OptionContractSymbolRegex = new(
        @"^([A-Z]{1,6})\d{6}[CP]\d{8}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant
    );
    private static readonly Regex OptionContractSymbolInTextRegex = new(
        @"[A-Z]{1,6}\d{6}[CP]\d{8}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant
    );

    private readonly IOptions<AlpacaTradingOptions> _alpacaOptions;
    private readonly ILogger<TradingAutomationBackgroundService> _logger;
    private readonly IOptions<TradingAutomationOptions> _options;
    private readonly IOptions<OpenAiTradingOptions> _openAiOptions;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ITradingAutomationStateStore _stateStore;
    private readonly ITradingLiveTelemetryChannel _liveTelemetryChannel;
    private readonly ITradingSentimentProgressChannel _sentimentProgressChannel;
    private readonly ITradingSentimentResultStore _sentimentResultStore;
    private readonly IAlpacaStreamingCache _streamingCache;
    private readonly IPreMarketScanTrigger _preMarketScanTrigger;
    private readonly Dictionary<string, OpportunityRuntimeState> _watchStates = new(
        StringComparer.OrdinalIgnoreCase
    );
    private static readonly TimeSpan FeeSyncInterval = TimeSpan.FromMinutes(15);

    private readonly TimeZoneInfo _tradingTimeZone;
    private DateOnly? _lastSentimentScanDate;
    private DateOnly? _lastResolvedMarketOpenDate;
    private DateTimeOffset? _lastResolvedMarketOpenUtc;
    private DateOnly? _lastStateResetDate;
    private bool _loggedDisabledMessage;
    private bool _loggedMissingApiCredentials;
    private bool _loggedMissingConfiguration;
    private DateTimeOffset? _lastFeeSyncAtUtc;
    private IReadOnlyCollection<TradingFeeActivitySnapshot> _latestFeeActivities = [];
    private string? _latestFeeCurrency;

    public TradingAutomationBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<AlpacaTradingOptions> alpacaOptions,
        IOptions<OpenAiTradingOptions> openAiOptions,
        IOptions<TradingAutomationOptions> options,
        ITradingAutomationStateStore stateStore,
        ITradingLiveTelemetryChannel liveTelemetryChannel,
        ITradingSentimentProgressChannel sentimentProgressChannel,
        ITradingSentimentResultStore sentimentResultStore,
        IAlpacaStreamingCache streamingCache,
        IPreMarketScanTrigger preMarketScanTrigger,
        ILogger<TradingAutomationBackgroundService> logger
    )
    {
        _scopeFactory = scopeFactory;
        _alpacaOptions = alpacaOptions;
        _openAiOptions = openAiOptions;
        _options = options;
        _stateStore = stateStore;
        _liveTelemetryChannel = liveTelemetryChannel;
        _sentimentProgressChannel = sentimentProgressChannel;
        _sentimentResultStore = sentimentResultStore;
        _streamingCache = streamingCache;
        _preMarketScanTrigger = preMarketScanTrigger;
        _logger = logger;
        _tradingTimeZone = ResolveTradingTimeZone(options.Value.TimeZoneId);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Trading automation tick failed.");
            }

            var delaySeconds = Math.Max(5, _options.Value.PollIntervalSeconds);
            await Task.Delay(TimeSpan.FromSeconds(delaySeconds), stoppingToken);
        }
    }

    private async Task TickAsync(CancellationToken cancellationToken)
    {
        var tickId = Guid.NewGuid().ToString("N")[..8];
        DateOnly? telemetryTradingDate = _lastStateResetDate;
        DateTimeOffset? telemetryMarketOpenUtc = _lastResolvedMarketOpenUtc;
        var telemetryMarketIsOpen = false;
        var telemetryWorkerEnabled = _options.Value.Enabled;

        using var scope = _scopeFactory.CreateScope();
        var dataProvider = scope.ServiceProvider.GetRequiredService<ITradingDataProvider>();
        var tradingSignalAgent = scope.ServiceProvider.GetRequiredService<ITradingSignalAgent>();
        var strategy = scope.ServiceProvider.GetRequiredService<RangeBreakoutRetestStrategy>();
        var tradePersistence = scope.ServiceProvider.GetRequiredService<ITradingTradePersistenceService>();

        _logger.LogInformation(
            "TradingAutomation tick START {TickId}. WatchStates={WatchStatesCount}.",
            tickId,
            _watchStates.Count
        );

        try
        {
            var options = _options.Value;
            telemetryWorkerEnabled = options.Enabled;
            if (!options.Enabled)
            {
                if (!_loggedDisabledMessage)
                {
                    _logger.LogInformation("Trading automation worker is disabled by configuration.");
                    _loggedDisabledMessage = true;
                }

                _logger.LogInformation(
                    "TradingAutomation tick EARLY-END {TickId}. Reason=WorkerDisabled.",
                    tickId
                );
                return;
            }

            if (string.IsNullOrWhiteSpace(options.WatchlistId) || options.OrderQuantity <= 0)
            {
                if (!_loggedMissingConfiguration)
                {
                    _logger.LogWarning(
                        "Trading automation worker requires WatchlistId and OrderQuantity > 0."
                    );
                    _loggedMissingConfiguration = true;
                }

                _logger.LogInformation(
                    "TradingAutomation tick EARLY-END {TickId}. Reason=MissingConfiguration.",
                    tickId
                );
                return;
            }

            if (
                string.IsNullOrWhiteSpace(_alpacaOptions.Value.ApiKey)
                || string.IsNullOrWhiteSpace(_alpacaOptions.Value.ApiSecret)
                || string.IsNullOrWhiteSpace(_openAiOptions.Value.ApiKey)
            )
            {
                if (!_loggedMissingApiCredentials)
                {
                    _logger.LogWarning(
                        "Trading automation is enabled, but Alpaca or OpenAI credentials are missing."
                    );
                    _loggedMissingApiCredentials = true;
                }

                _logger.LogInformation(
                    "TradingAutomation tick EARLY-END {TickId}. Reason=MissingApiCredentials.",
                    tickId
                );
                return;
            }

            var utcNow = DateTimeOffset.UtcNow;
            var tradingNow = TimeZoneInfo.ConvertTime(utcNow, _tradingTimeZone);
            var tradingDate = DateOnly.FromDateTime(tradingNow.Date);
            telemetryTradingDate = tradingDate;
            var sentimentScanTime = new TimeSpan(
                Math.Clamp(options.SentimentScanHour, 0, 23),
                Math.Clamp(options.SentimentScanMinute, 0, 59),
                0
            );

            if (_lastStateResetDate is null || _lastStateResetDate.Value != tradingDate)
            {
                _lastResolvedMarketOpenDate = null;
                _lastResolvedMarketOpenUtc = null;
                _lastStateResetDate = tradingDate;
                await RunStepAsync(
                    tickId,
                    "RestoreState",
                    async () => await RestoreStateAsync(tradingDate, cancellationToken)
                );
            }

            var forceScan = _preMarketScanTrigger.TryConsume();
            if (forceScan)
            {
                _lastSentimentScanDate = null;
                _logger.LogInformation(
                    "TradingAutomation tick {TickId}: Pre-market scan manually triggered; forcing re-scan.",
                    tickId
                );
            }

            if (
                (forceScan || tradingNow.TimeOfDay >= sentimentScanTime)
                && (_lastSentimentScanDate is null || _lastSentimentScanDate.Value != tradingDate)
            )
            {
                await RunStepAsync(
                    tickId,
                    "RefreshDailyOpportunities",
                    async () =>
                        await RefreshDailyOpportunitiesAsync(
                            dataProvider,
                            tradingSignalAgent,
                            tradingDate,
                            cancellationToken
                        )
                );
            }

            await RunStepAsync(
                tickId,
                "SyncClosedTradeFees",
                async () =>
                    await SyncClosedTradeFeesAsync(
                        dataProvider,
                        tradePersistence,
                        utcNow,
                        cancellationToken
                    )
            );

            var stateChanged = PruneCompletedWatchStates();

            if (_watchStates.Count == 0)
            {
                _streamingCache.SetSymbols([]);
                if (stateChanged)
                {
                    await RunStepAsync(
                        tickId,
                        "PersistState",
                        async () => await PersistStateAsync(cancellationToken)
                    );
                }
                _logger.LogInformation(
                    "TradingAutomation tick EARLY-END {TickId}. Reason=NoWatchStatesAfterPrune.",
                    tickId
                );
                return;
            }

            _streamingCache.SetSymbols(_watchStates.Keys.ToArray());

            var marketOpenUtc = await RunStepAsync(
                tickId,
                "ResolveMarketOpenUtc",
                async () =>
                    await ResolveMarketOpenUtcAsync(
                        dataProvider,
                        tradingDate,
                        cancellationToken
                    )
            );
            telemetryMarketOpenUtc = marketOpenUtc;
            if (utcNow < marketOpenUtc)
            {
                if (stateChanged)
                {
                    await RunStepAsync(
                        tickId,
                        "PersistState",
                        async () => await PersistStateAsync(cancellationToken)
                    );
                }
                _logger.LogInformation(
                    "TradingAutomation tick EARLY-END {TickId}. Reason=BeforeMarketOpen MarketOpenUtc={MarketOpenUtc} UtcNow={UtcNow}.",
                    tickId,
                    marketOpenUtc,
                    utcNow
                );
                return;
            }

            var marketClock = await RunStepAsync(
                tickId,
                "GetMarketClock",
                async () => await dataProvider.GetMarketClockAsync(cancellationToken)
            );
            telemetryMarketIsOpen = marketClock.IsOpen;
            if (!marketClock.IsOpen)
            {
                if (stateChanged)
                {
                    await RunStepAsync(
                        tickId,
                        "PersistState",
                        async () => await PersistStateAsync(cancellationToken)
                    );
                }
                _logger.LogInformation(
                    "TradingAutomation tick EARLY-END {TickId}. Reason=MarketClosed NextOpenUtc={NextOpenUtc} TimestampUtc={TimestampUtc}.",
                    tickId,
                    marketClock.NextOpen,
                    marketClock.Timestamp
                );
                return;
            }

            var openPositionsTask = RunStepAsync(
                tickId,
                "GetOpenPositions",
                async () => await dataProvider.GetPositionsAsync(cancellationToken)
            );
            var openOrdersTask = RunStepAsync(
                tickId,
                "GetOpenOrders",
                async () => await dataProvider.GetOpenOrdersAsync(cancellationToken)
            );
            await Task.WhenAll(openPositionsTask, openOrdersTask);
            var openPositions = openPositionsTask.Result;
            var openOrders = openOrdersTask.Result;

            stateChanged =
                await RunStepAsync(
                    tickId,
                    "AuditActiveOrders",
                    async () =>
                        await AuditActiveOrdersAsync(
                            dataProvider,
                            tradePersistence,
                            marketOpenUtc,
                            cancellationToken
                        )
                )
                || stateChanged;

            if (PruneCompletedWatchStates())
            {
                stateChanged = true;
                _streamingCache.SetSymbols(
                    _watchStates.Count == 0 ? [] : _watchStates.Keys.ToArray()
                );
            }

            if (_watchStates.Count == 0)
            {
                if (stateChanged)
                {
                    await RunStepAsync(
                        tickId,
                        "PersistState",
                        async () => await PersistStateAsync(cancellationToken)
                    );
                }
                _logger.LogInformation(
                    "TradingAutomation tick EARLY-END {TickId}. Reason=NoWatchStatesAfterAudit.",
                    tickId
                );
                return;
            }

            foreach (var state in _watchStates.Values.Where(x => !x.OrderPlaced).ToArray())
            {
                try
                {
                    stateChanged =
                        await RunStepAsync(
                            tickId,
                            "EvaluateOpportunity",
                            async () =>
                                await EvaluateOpportunityAsync(
                                    strategy,
                                    dataProvider,
                                    tradingSignalAgent,
                                    state,
                                    tradingDate,
                                    marketOpenUtc,
                                    openPositions,
                                    openOrders,
                                    tradePersistence,
                                    cancellationToken
                                ),
                            state.Opportunity.Symbol
                        )
                        || stateChanged;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Failed to evaluate opportunity for {Symbol}; continuing with remaining symbols.",
                        state.Opportunity.Symbol
                    );
                }
            }

            if (stateChanged)
            {
                await RunStepAsync(
                    tickId,
                    "PersistState",
                    async () => await PersistStateAsync(cancellationToken)
                );
            }
        }
        finally
        {
            TryPublishLiveTelemetrySnapshot(
                telemetryTradingDate,
                telemetryMarketOpenUtc,
                telemetryMarketIsOpen,
                telemetryWorkerEnabled
            );
            _logger.LogInformation(
                "TradingAutomation tick END {TickId}. WatchStates={WatchStatesCount}.",
                tickId,
                _watchStates.Count
            );
        }
    }

    private async Task RunStepAsync(
        string tickId,
        string stepName,
        Func<Task> action,
        string? symbol = null
    )
    {
        var sw = Stopwatch.StartNew();
        if (!string.IsNullOrWhiteSpace(symbol))
        {
            _logger.LogInformation(
                "TradingAutomation step START {TickId} {Step} Symbol={Symbol}.",
                tickId,
                stepName,
                symbol
            );
        }
        else
        {
            _logger.LogInformation("TradingAutomation step START {TickId} {Step}.", tickId, stepName);
        }

        try
        {
            await action();
        }
        finally
        {
            sw.Stop();
            if (!string.IsNullOrWhiteSpace(symbol))
            {
                _logger.LogInformation(
                    "TradingAutomation step END {TickId} {Step} Symbol={Symbol} DurationMs={DurationMs}.",
                    tickId,
                    stepName,
                    symbol,
                    sw.ElapsedMilliseconds
                );
            }
            else
            {
                _logger.LogInformation(
                    "TradingAutomation step END {TickId} {Step} DurationMs={DurationMs}.",
                    tickId,
                    stepName,
                    sw.ElapsedMilliseconds
                );
            }
        }
    }

    private async Task<T> RunStepAsync<T>(
        string tickId,
        string stepName,
        Func<Task<T>> action,
        string? symbol = null
    )
    {
        var sw = Stopwatch.StartNew();
        if (!string.IsNullOrWhiteSpace(symbol))
        {
            _logger.LogInformation(
                "TradingAutomation step START {TickId} {Step} Symbol={Symbol}.",
                tickId,
                stepName,
                symbol
            );
        }
        else
        {
            _logger.LogInformation("TradingAutomation step START {TickId} {Step}.", tickId, stepName);
        }

        try
        {
            return await action();
        }
        finally
        {
            sw.Stop();
            if (!string.IsNullOrWhiteSpace(symbol))
            {
                _logger.LogInformation(
                    "TradingAutomation step END {TickId} {Step} Symbol={Symbol} DurationMs={DurationMs}.",
                    tickId,
                    stepName,
                    symbol,
                    sw.ElapsedMilliseconds
                );
            }
            else
            {
                _logger.LogInformation(
                    "TradingAutomation step END {TickId} {Step} DurationMs={DurationMs}.",
                    tickId,
                    stepName,
                    sw.ElapsedMilliseconds
                );
            }
        }
    }

    private bool PruneCompletedWatchStates()
    {
        var completedSymbols = _watchStates
            .Where(x => x.Value.OrderPlaced && x.Value.ExitAuditLogged)
            .Select(x => x.Key)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (completedSymbols.Length == 0)
        {
            return false;
        }

        foreach (var symbol in completedSymbols)
        {
            _watchStates.Remove(symbol);
        }

        _logger.LogInformation(
            "Stopped monitoring completed symbols for {TradingDate}: {Symbols}.",
            _lastStateResetDate,
            string.Join(", ", completedSymbols)
        );

        return true;
    }

    private async Task RestoreStateAsync(DateOnly tradingDate, CancellationToken cancellationToken)
    {
        _watchStates.Clear();
        _lastSentimentScanDate = null;

        var snapshot = await _stateStore.LoadAsync(cancellationToken);
        if (snapshot is null || snapshot.TradingDate != tradingDate)
        {
            return;
        }

        foreach (var symbolState in snapshot.Symbols)
        {
            if (string.IsNullOrWhiteSpace(symbolState.Opportunity.Symbol))
            {
                continue;
            }

            _watchStates[symbolState.Opportunity.Symbol] = new OpportunityRuntimeState(
                symbolState.Opportunity
            )
            {
                OpeningRange = symbolState.OpeningRange,
                BreakoutBar = symbolState.BreakoutBar,
                LastEvaluatedRetestTimestamp = symbolState.LastEvaluatedRetestTimestamp,
                OrderPlaced = symbolState.OrderPlaced,
                SentimentAnalysisId = symbolState.SentimentAnalysisId,
                OrderId = symbolState.OrderId,
                OrderSubmittedAtUtc = symbolState.OrderSubmittedAtUtc,
                EntrySignalBarTimestampUtc = symbolState.EntrySignalBarTimestampUtc,
                PlannedEntryPrice = symbolState.PlannedEntryPrice,
                StopLossPrice = symbolState.StopLossPrice,
                TakeProfitPrice = symbolState.TakeProfitPrice,
                EntryAuditLogged = symbolState.EntryAuditLogged,
                ExitAuditLogged = symbolState.ExitAuditLogged,
                EntryFilledAtUtc = symbolState.EntryFilledAtUtc,
                ExitFilledAtUtc = symbolState.ExitFilledAtUtc,
                EntryBarTimestampUtc = symbolState.EntryBarTimestampUtc,
                ExitBarTimestampUtc = symbolState.ExitBarTimestampUtc,
                EntryBarIndex = symbolState.EntryBarIndex,
                ExitBarIndex = symbolState.ExitBarIndex,
                OrderSubmissionRejected = symbolState.OrderSubmissionRejected,
                LastOrderSubmissionError = symbolState.LastOrderSubmissionError,
                LastOrderSubmissionFailedAtUtc = symbolState.LastOrderSubmissionFailedAtUtc,
                TradedInstrumentSymbol = symbolState.TradedInstrumentSymbol,
                OptionContractType = symbolState.OptionContractType,
                OptionStrikePrice = symbolState.OptionStrikePrice,
                OptionExpirationDate = symbolState.OptionExpirationDate,
                PendingExitOrderId = symbolState.PendingExitOrderId,
                PendingExitReason = symbolState.PendingExitReason,
                OptionStopLossPrice = symbolState.OptionStopLossPrice,
                OptionTakeProfitPrice = symbolState.OptionTakeProfitPrice,
                OptionStopLossOrderId = symbolState.OptionStopLossOrderId,
                OptionTakeProfitOrderId = symbolState.OptionTakeProfitOrderId,
                UseTrailingStopLoss = symbolState.UseTrailingStopLoss,
                InitialRiskPerUnit = symbolState.InitialRiskPerUnit,
                PlannedPartialTakeProfitQuantity = symbolState.PlannedPartialTakeProfitQuantity,
                PlannedRunnerQuantity = symbolState.PlannedRunnerQuantity,
                RemainingRunnerQuantity = symbolState.RemainingRunnerQuantity,
                EquityStopLossOrderId = symbolState.EquityStopLossOrderId,
                EquityTrailingStopOrderId = symbolState.EquityTrailingStopOrderId,
                PartialTakeProfitOrderId = symbolState.PartialTakeProfitOrderId,
                PartialTakeProfitFilled = symbolState.PartialTakeProfitFilled,
                PartialTakeProfitFilledAtUtc = symbolState.PartialTakeProfitFilledAtUtc,
                LiveTrailingStopPrice = symbolState.LiveTrailingStopPrice,
                LiveTrailingStopActivatedAtUtc = symbolState.LiveTrailingStopActivatedAtUtc,
            };

            foreach (var retestAttempt in symbolState.RetestAttempts ?? [])
            {
                _watchStates[symbolState.Opportunity.Symbol].RetestAttempts.Add(
                    new RetestAttemptRuntimeState(
                        retestAttempt.AttemptId,
                        retestAttempt.RetestBar,
                        retestAttempt.IsValid,
                        retestAttempt.Score,
                        retestAttempt.RejectionReason,
                        retestAttempt.Validation
                    )
                );
            }
        }

        _lastSentimentScanDate = snapshot.LastSentimentScanDate;
        _logger.LogInformation(
            "Trading automation state restored for {TradingDate}: {Count} symbols.",
            tradingDate,
            _watchStates.Count
        );
    }

    private async Task PersistStateAsync(CancellationToken cancellationToken)
    {
        if (_lastStateResetDate is null)
        {
            return;
        }

        var symbols = _watchStates.Values
            .Select(x => new TradingAutomationSymbolStateSnapshot(
                x.Opportunity,
                x.OpeningRange,
                x.BreakoutBar,
                x.RetestAttempts
                    .Select(attempt => new TradingAutomationRetestAttemptStateSnapshot(
                        attempt.AttemptId,
                        attempt.RetestBar,
                        attempt.IsValid,
                        attempt.Score,
                        attempt.RejectionReason,
                        attempt.Validation
                    ))
                    .ToArray(),
                x.LastEvaluatedRetestTimestamp,
                x.OrderPlaced,
                x.SentimentAnalysisId,
                x.OrderId,
                x.OrderSubmittedAtUtc,
                x.EntrySignalBarTimestampUtc,
                x.PlannedEntryPrice,
                x.StopLossPrice,
                x.TakeProfitPrice,
                x.EntryAuditLogged,
                x.ExitAuditLogged,
                x.EntryFilledAtUtc,
                x.ExitFilledAtUtc,
                x.EntryBarTimestampUtc,
                x.ExitBarTimestampUtc,
                x.EntryBarIndex,
                x.ExitBarIndex,
                x.OrderSubmissionRejected,
                x.LastOrderSubmissionError,
                x.LastOrderSubmissionFailedAtUtc,
                x.TradedInstrumentSymbol,
                x.OptionContractType,
                x.OptionStrikePrice,
                x.OptionExpirationDate,
                x.PendingExitOrderId,
                x.PendingExitReason,
                x.OptionStopLossPrice,
                x.OptionTakeProfitPrice,
                x.OptionStopLossOrderId,
                x.OptionTakeProfitOrderId,
                x.UseTrailingStopLoss,
                x.InitialRiskPerUnit,
                x.PlannedPartialTakeProfitQuantity,
                x.PlannedRunnerQuantity,
                x.RemainingRunnerQuantity,
                x.EquityStopLossOrderId,
                x.EquityTrailingStopOrderId,
                x.PartialTakeProfitOrderId,
                x.PartialTakeProfitFilled,
                x.PartialTakeProfitFilledAtUtc,
                x.LiveTrailingStopPrice,
                x.LiveTrailingStopActivatedAtUtc
            ))
            .ToArray();

        await _stateStore.SaveAsync(
            new TradingAutomationStateSnapshot(_lastStateResetDate.Value, _lastSentimentScanDate, symbols),
            cancellationToken
        );
    }

    private void TryPublishLiveTelemetrySnapshot(
        DateOnly? tradingDate,
        DateTimeOffset? marketOpenUtc,
        bool marketIsOpen,
        bool workerEnabled
    )
    {
        try
        {
            var generatedAtUtc = DateTimeOffset.UtcNow;
            var symbolStates = _watchStates
                .Values.OrderBy(x => x.Opportunity.Symbol, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var symbols = symbolStates
                .Select(x =>
                    BuildLiveTelemetrySymbolSnapshot(x, tradingDate, marketOpenUtc, generatedAtUtc)
                )
                .ToArray();
            var fees = BuildLiveFeesSnapshot(symbolStates);

            _liveTelemetryChannel.TryPublish(
                new TradingLiveSnapshot(
                    generatedAtUtc,
                    tradingDate,
                    _lastSentimentScanDate,
                    workerEnabled,
                    marketOpenUtc,
                    marketIsOpen,
                    symbols
                )
                {
                    LastSentimentResult = _sentimentResultStore.GetLatest(),
                    Fees = fees,
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to publish trading live telemetry snapshot.");
        }
    }

    private TradingLiveSymbolSnapshot BuildLiveTelemetrySymbolSnapshot(
        OpportunityRuntimeState state,
        DateOnly? tradingDate,
        DateTimeOffset? marketOpenUtc,
        DateTimeOffset generatedAtUtc
    )
    {
        var sessionStartUtc = ResolveTelemetrySessionStartUtc(tradingDate, marketOpenUtc, generatedAtUtc);
        var sessionBars = _streamingCache
            .GetBars(state.Opportunity.Symbol, sessionStartUtc, generatedAtUtc, 500)
            .OrderBy(x => x.Timestamp)
            .ToArray();
        if (sessionBars.Length == 0 && state.LastSessionBars is { Length: > 0 } cachedBars)
        {
            sessionBars = cachedBars
                .Where(x => x.Timestamp >= sessionStartUtc && x.Timestamp <= generatedAtUtc)
                .OrderBy(x => x.Timestamp)
                .ToArray();
        }

        decimal? lastPrice = null;
        if (_streamingCache.TryGetQuote(state.Opportunity.Symbol, out var quote))
        {
            lastPrice = quote.LastPrice > 0m ? quote.LastPrice : null;
        }

        var retestAttempts = state.RetestAttempts
            .OrderBy(x => x.RetestBar.Timestamp)
            .Select(attempt =>
                new TradingLiveRetestAttemptSnapshot(
                    attempt.AttemptId,
                    attempt.RetestBar.Timestamp,
                    FindBarIndexByTimestamp(sessionBars, attempt.RetestBar.Timestamp),
                    attempt.IsValid,
                    attempt.Score,
                    attempt.RejectionReason,
                    attempt.Validation
                )
            )
            .ToArray();

        return new TradingLiveSymbolSnapshot(
            state.Opportunity.Symbol,
            state.Opportunity.Direction,
            state.Opportunity.Score,
            state.Opportunity.SignalInsights,
            ResolveLifecycleState(state),
            state.OrderPlaced,
            state.EntryAuditLogged,
            state.ExitAuditLogged,
            state.OrderSubmissionRejected,
            state.OrderId,
            state.OrderSubmittedAtUtc,
            state.PlannedEntryPrice,
            state.StopLossPrice,
            state.TakeProfitPrice,
            state.OpeningRange is null
                ? null
                : new OpeningRangeSnapshotDto(
                    state.OpeningRange.StartTime,
                    state.OpeningRange.EndTime,
                    state.OpeningRange.Upper,
                    state.OpeningRange.Lower
                ),
            state.BreakoutBar?.Timestamp,
            state.LastEvaluatedRetestTimestamp,
            retestAttempts,
            state.EntryFilledAtUtc,
            state.ExitFilledAtUtc,
            state.TradedInstrumentSymbol,
            state.OptionContractType,
            state.OptionStrikePrice,
            state.OptionExpirationDate,
            state.PendingExitOrderId,
            state.PendingExitReason,
            state.EntryBarTimestampUtc,
            state.ExitBarTimestampUtc,
            state.EntryBarIndex,
            state.ExitBarIndex,
            state.LastOrderSubmissionError,
            state.LastOrderSubmissionFailedAtUtc,
            lastPrice,
            sessionBars
        );
    }

    private TradingLiveFeesSnapshot BuildLiveFeesSnapshot(
        IReadOnlyCollection<OpportunityRuntimeState> symbolStates
    )
    {
        var feeActivities = _latestFeeActivities;
        var estimatedSpreadBps = Math.Max(0m, _options.Value.BacktestEstimatedSpreadBps);
        var commissionPerUnit = Math.Max(0m, _options.Value.BacktestCommissionPerUnit);
        var configuredOrderQuantity = Math.Max(0m, _options.Value.OrderQuantity);

        var symbolFees = new Dictionary<string, SymbolFeeAccumulator>(StringComparer.OrdinalIgnoreCase);
        var orderIdToSymbol = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var symbolTokens = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var state in symbolStates)
        {
            var symbol = state.Opportunity.Symbol.Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(symbol))
            {
                continue;
            }

            symbolFees[symbol] = new SymbolFeeAccumulator(symbol);
            symbolTokens[symbol] = BuildFeeLookupTokens(state);

            TryMapOrderId(orderIdToSymbol, state.OrderId, symbol);
            TryMapOrderId(orderIdToSymbol, state.PendingExitOrderId, symbol);
            TryMapOrderId(orderIdToSymbol, state.OptionStopLossOrderId, symbol);
            TryMapOrderId(orderIdToSymbol, state.OptionTakeProfitOrderId, symbol);
            TryMapOrderId(orderIdToSymbol, state.EquityStopLossOrderId, symbol);
            TryMapOrderId(orderIdToSymbol, state.EquityTrailingStopOrderId, symbol);
            TryMapOrderId(orderIdToSymbol, state.PartialTakeProfitOrderId, symbol);
        }

        decimal recentTotalFees = 0m;
        decimal recentOptionFees = 0m;
        decimal recentEquityFees = 0m;

        foreach (var activity in feeActivities)
        {
            var feeAmount = Math.Abs(activity.NetAmount);
            if (feeAmount <= 0m)
            {
                continue;
            }

            var isOptionFee = IsOptionFeeActivity(activity);
            recentTotalFees += feeAmount;
            if (isOptionFee)
            {
                recentOptionFees += feeAmount;
            }
            else
            {
                recentEquityFees += feeAmount;
            }

            var symbol = ResolveFeeSymbol(activity, orderIdToSymbol, symbolTokens);
            if (symbol is null || !symbolFees.TryGetValue(symbol, out var accumulator))
            {
                continue;
            }

            accumulator.ActivityCount++;
            accumulator.TotalFees += feeAmount;
            if (isOptionFee)
            {
                accumulator.OptionFees += feeAmount;
            }
            else
            {
                accumulator.EquityFees += feeAmount;
            }
        }

        var symbolSnapshots = symbolFees
            .Values.OrderBy(x => x.Symbol, StringComparer.OrdinalIgnoreCase)
            .Select(x =>
                new TradingLiveSymbolFeeSnapshot(
                    x.Symbol,
                    decimal.Round(x.TotalFees, 6),
                    decimal.Round(x.OptionFees, 6),
                    decimal.Round(x.EquityFees, 6),
                    x.ActivityCount
                )
            )
            .ToArray();

        return new TradingLiveFeesSnapshot(
            _lastFeeSyncAtUtc,
            estimatedSpreadBps,
            commissionPerUnit,
            configuredOrderQuantity,
            decimal.Round(recentTotalFees, 6),
            decimal.Round(recentOptionFees, 6),
            decimal.Round(recentEquityFees, 6),
            _latestFeeCurrency,
            symbolSnapshots
        );
    }

    private static HashSet<string> BuildFeeLookupTokens(OpportunityRuntimeState state)
    {
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(state.Opportunity.Symbol))
        {
            tokens.Add(state.Opportunity.Symbol.Trim().ToUpperInvariant());
        }

        if (!string.IsNullOrWhiteSpace(state.TradedInstrumentSymbol))
        {
            var instrument = state.TradedInstrumentSymbol.Trim().ToUpperInvariant();
            tokens.Add(instrument);
            if (TryExtractUnderlyingFromOptionContractSymbol(instrument, out var underlying))
            {
                tokens.Add(underlying);
            }
        }

        return tokens;
    }

    private static void TryMapOrderId(
        IDictionary<string, string> orderIdToSymbol,
        string? orderId,
        string symbol
    )
    {
        if (string.IsNullOrWhiteSpace(orderId) || string.IsNullOrWhiteSpace(symbol))
        {
            return;
        }

        orderIdToSymbol[orderId.Trim()] = symbol;
    }

    private static bool IsOptionFeeActivity(TradingFeeActivitySnapshot activity)
    {
        var description = activity.Description?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(description))
        {
            return false;
        }

        if (
            description.Contains("CONTRACT", StringComparison.OrdinalIgnoreCase)
            || description.Contains("OPTION", StringComparison.OrdinalIgnoreCase)
        )
        {
            return true;
        }

        return OptionContractSymbolInTextRegex.IsMatch(description);
    }

    private static string? ResolveFeeSymbol(
        TradingFeeActivitySnapshot activity,
        IReadOnlyDictionary<string, string> orderIdToSymbol,
        IReadOnlyDictionary<string, HashSet<string>> symbolTokens
    )
    {
        if (
            !string.IsNullOrWhiteSpace(activity.OrderId)
            && orderIdToSymbol.TryGetValue(activity.OrderId.Trim(), out var orderMappedSymbol)
        )
        {
            return orderMappedSymbol;
        }

        var description = activity.Description?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(description))
        {
            return null;
        }

        foreach (var pair in symbolTokens.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            foreach (var symbolToken in pair.Value)
            {
                if (ContainsSymbolToken(description, symbolToken))
                {
                    return pair.Key;
                }
            }
        }

        return null;
    }

    private static bool ContainsSymbolToken(string text, string symbolToken)
    {
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(symbolToken))
        {
            return false;
        }

        var pattern = $@"(?<![A-Z0-9]){Regex.Escape(symbolToken)}(?![A-Z0-9])";
        return Regex.IsMatch(text, pattern, RegexOptions.CultureInvariant);
    }

    private DateTimeOffset ResolveTelemetrySessionStartUtc(
        DateOnly? tradingDate,
        DateTimeOffset? marketOpenUtc,
        DateTimeOffset generatedAtUtc
    )
    {
        if (marketOpenUtc is DateTimeOffset resolvedMarketOpenUtc)
        {
            return resolvedMarketOpenUtc;
        }

        var tradingDay =
            tradingDate
            ?? DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(generatedAtUtc, _tradingTimeZone).Date);
        return ToMarketDateTimeUtc(tradingDay, FallbackMarketOpenHour, FallbackMarketOpenMinute);
    }

    private static string ResolveLifecycleState(OpportunityRuntimeState state)
    {
        if (state.ExitAuditLogged)
        {
            return "Closed";
        }

        if (!string.IsNullOrWhiteSpace(state.EquityTrailingStopOrderId))
        {
            return "RunnerTrailingStopActive";
        }

        if (!string.IsNullOrWhiteSpace(state.PartialTakeProfitOrderId))
        {
            return "PartialTakeProfitPending";
        }

        if (!string.IsNullOrWhiteSpace(state.EquityStopLossOrderId))
        {
            return "StopLossActive";
        }

        if (!string.IsNullOrWhiteSpace(state.PendingExitOrderId))
        {
            return "ExitPending";
        }

        if (state.EntryAuditLogged)
        {
            return "InPosition";
        }

        if (state.OrderPlaced)
        {
            return "OrderSubmitted";
        }

        if (state.OrderSubmissionRejected)
        {
            return "OrderRejected";
        }

        if (state.BreakoutBar is not null)
        {
            return "BreakoutDetected";
        }

        if (state.LastEvaluatedRetestTimestamp is not null)
        {
            return "RetestEvaluated";
        }

        if (state.OpeningRange is not null)
        {
            return "AwaitingBreakout";
        }

        return "Scanning";
    }

    private async Task RefreshDailyOpportunitiesAsync(
        ITradingDataProvider dataProvider,
        ITradingSignalAgent tradingSignalAgent,
        DateOnly tradingDate,
        CancellationToken cancellationToken
    )
    {
        var options = _options.Value;

        PublishSentimentProgress("scanning", "Watchlist wird abgerufen und geprüft…", null, null, null);

        var symbols = await dataProvider.GetWatchlistSymbolsAsync(options.WatchlistId, cancellationToken);
        if (symbols.Count == 0)
        {
            _logger.LogWarning(
                "Watchlist {WatchlistId} returned no symbols for sentiment analysis.",
                options.WatchlistId
            );
            _watchStates.Clear();
            _lastSentimentScanDate = tradingDate;
            await PersistStateAsync(cancellationToken);
            PublishSentimentProgress("none_found", "Watchlist enthält keine Symbole – heute keine Trades.", 0, 0, null);
            return;
        }

        IReadOnlyCollection<string> eligibleSymbols = symbols;
        if (options.UseOptionsTrading)
        {
            PublishSentimentProgress(
                "scanning",
                $"Options-Verfügbarkeit wird für {symbols.Count} Symbol(e) geprüft…",
                symbols.Count,
                null,
                null
            );

            eligibleSymbols = await FilterOptionEligibleSymbolsAsync(
                dataProvider,
                symbols,
                tradingDate,
                options,
                cancellationToken
            );
            if (eligibleSymbols.Count == 0)
            {
                _logger.LogWarning(
                    "Watchlist {WatchlistId} has no symbols with both call and put option support in configured DTE range ({MinDte}-{MaxDte}).",
                    options.WatchlistId,
                    Math.Max(0, options.OptionMinDaysToExpiration),
                    Math.Max(Math.Max(0, options.OptionMinDaysToExpiration), options.OptionMaxDaysToExpiration)
                );
                _watchStates.Clear();
                _lastSentimentScanDate = tradingDate;
                await PersistStateAsync(cancellationToken);
                PublishSentimentProgress(
                    "none_found",
                    "Keine Symbole mit Options-Support im konfigurierten DTE-Fenster – heute keine Trades.",
                    symbols.Count,
                    0,
                    null
                );
                return;
            }
        }

        PublishSentimentProgress(
            "analyzing",
            $"KI analysiert Sentiment und Candle-Struktur für {eligibleSymbols.Count} Symbol(e)…",
            eligibleSymbols.Count,
            null,
            null
        );

        var maxOpportunities = Math.Clamp(options.MaxOpportunities, 1, 20);
        var minOpportunities = Math.Clamp(options.MinOpportunities, 1, maxOpportunities);
        var agentTextBuilder = new System.Text.StringBuilder();
        var opportunities = await tradingSignalAgent.AnalyzeWatchlistSentimentAsync(
            eligibleSymbols,
            minOpportunities,
            maxOpportunities,
            tradingDate,
            cancellationToken,
            onStreamingActivityDelta: chunk =>
            {
                agentTextBuilder.Append(chunk);
                PublishSentimentProgress(
                    "analyzing",
                    $"KI analysiert… ({eligibleSymbols.Count} Symbol(e))",
                    eligibleSymbols.Count,
                    null,
                    agentTextBuilder.ToString()
                );
            }
        );

        var agentText = agentTextBuilder.Length > 0 ? agentTextBuilder.ToString() : null;
        var orderedOpportunities = opportunities
            .OrderByDescending(x => x.Score)
            .ToArray();
        var selected = orderedOpportunities
            .Where(x => x.Score >= options.MinimumSentimentScore)
            .Take(maxOpportunities)
            .ToList();

        if (selected.Count < minOpportunities)
        {
            var selectedSymbols = selected
                .Select(x => x.Symbol)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var fallback = orderedOpportunities
                .Where(x => !selectedSymbols.Contains(x.Symbol))
                .Take(minOpportunities - selected.Count);
            selected.AddRange(fallback);
        }

        selected = selected
            .OrderByDescending(x => x.Score)
            .Take(maxOpportunities)
            .ToList();
        var selectedArray = selected.ToArray();

        var sentimentAnalysisId = await StoreSentimentAnalysisResult(
            opportunities,
            selectedArray,
            tradingDate,
            agentText,
            cancellationToken
        );

        _watchStates.Clear();
        foreach (var opportunity in selectedArray)
        {
            _watchStates[opportunity.Symbol] = new OpportunityRuntimeState(opportunity)
            {
                SentimentAnalysisId = sentimentAnalysisId,
            };
        }

        _lastSentimentScanDate = tradingDate;
        await PersistStateAsync(cancellationToken);

        if (selectedArray.Length > 0)
        {
            var symbolList = string.Join(", ", selectedArray.Select(x => x.Symbol));
            PublishSentimentProgress(
                "completed",
                $"Analyse abgeschlossen: {selectedArray.Length} Chance(n) gefunden – {symbolList}.",
                eligibleSymbols.Count,
                selectedArray.Length,
                null
            );
        }
        else
        {
            PublishSentimentProgress(
                "none_found",
                "Sentiment-Analyse ergab keine ausreichend starken Chancen – heute keine Trades.",
                eligibleSymbols.Count,
                0,
                null
            );
        }

        _logger.LogInformation(
            "Daily watchlist sentiment scan completed: {Payload}",
            JsonSerializer.Serialize(
                new
                {
                    Opportunities = selectedArray.Select(x => new
                    {
                        x.Symbol,
                        Direction = x.Direction.ToString(),
                        x.Score,
                    }),
                }
            )
        );
    }

    private void PublishSentimentProgress(
        string phase,
        string message,
        int? symbolCount,
        int? resultCount,
        string? agentStreamingText
    )
    {
        try
        {
            _sentimentProgressChannel.TryPublish(
                new TradingSentimentProgress(DateTimeOffset.UtcNow, phase, message, symbolCount, resultCount)
                {
                    AgentStreamingText = agentStreamingText,
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to publish sentiment progress event.");
        }
    }

    private async Task<long?> StoreSentimentAnalysisResult(
        IReadOnlyCollection<TradingOpportunity> allOpportunities,
        TradingOpportunity[] selectedOpportunities,
        DateOnly tradingDate,
        string? agentText,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var selectedSymbols = new HashSet<string>(
                selectedOpportunities.Select(x => x.Symbol),
                StringComparer.OrdinalIgnoreCase
            );
            var results = allOpportunities
                .Select(o => new TradingSentimentOpportunityResult(
                    o.Symbol,
                    o.Direction.ToString(),
                    o.Score,
                    selectedSymbols.Contains(o.Symbol),
                    o.SignalInsights
                ))
                .OrderByDescending(o => o.Score)
                .ToArray();

            return await _sentimentResultStore.SetLatestAsync(
                new TradingSentimentAnalysisResult(
                    DateTimeOffset.UtcNow,
                    tradingDate,
                    agentText,
                    results
                ),
                cancellationToken
            );
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to store sentiment analysis result.");
            return null;
        }
    }

    private async Task<IReadOnlyCollection<string>> FilterOptionEligibleSymbolsAsync(
        ITradingDataProvider dataProvider,
        IReadOnlyCollection<string> symbols,
        DateOnly tradingDate,
        TradingAutomationOptions options,
        CancellationToken cancellationToken
    )
    {
        var normalizedSymbols = symbols
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (normalizedSymbols.Length == 0)
        {
            return [];
        }

        var normalizedMinDte = Math.Max(0, options.OptionMinDaysToExpiration);
        var normalizedMaxDte = Math.Max(normalizedMinDte, options.OptionMaxDaysToExpiration);
        var optionEligibleSymbols = new List<string>(normalizedSymbols.Length);
        var nonEligibleSymbols = new List<string>();

        foreach (var symbol in normalizedSymbols)
        {
            try
            {
                var quote = _streamingCache.TryGetQuote(symbol, out var streamQuote)
                    ? streamQuote
                    : await dataProvider.GetQuoteAsync(symbol, cancellationToken);
                var referencePrice = ResolveUnderlyingReferencePrice(quote);
                if (referencePrice <= 0m)
                {
                    nonEligibleSymbols.Add(symbol);
                    continue;
                }

                var callContract = await dataProvider.SelectOptionContractAsync(
                    symbol,
                    TradingDirection.Bullish,
                    referencePrice,
                    tradingDate,
                    normalizedMinDte,
                    normalizedMaxDte,
                    cancellationToken
                );
                var putContract = await dataProvider.SelectOptionContractAsync(
                    symbol,
                    TradingDirection.Bearish,
                    referencePrice,
                    tradingDate,
                    normalizedMinDte,
                    normalizedMaxDte,
                    cancellationToken
                );

                if (callContract is not null && putContract is not null)
                {
                    optionEligibleSymbols.Add(symbol);
                }
                else
                {
                    nonEligibleSymbols.Add(symbol);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                nonEligibleSymbols.Add(symbol);
                _logger.LogWarning(
                    ex,
                    "Failed to verify option contract support for watchlist symbol {Symbol}; excluding it from options-only sentiment scan.",
                    symbol
                );
            }
        }

        if (nonEligibleSymbols.Count > 0)
        {
            _logger.LogWarning(
                "Watchlist {WatchlistId} symbols without two-sided option support in configured DTE range ({MinDte}-{MaxDte}) were skipped: {Symbols}.",
                options.WatchlistId,
                normalizedMinDte,
                normalizedMaxDte,
                string.Join(", ", nonEligibleSymbols)
            );
        }

        return optionEligibleSymbols.ToArray();
    }

    private async Task<bool> EvaluateOpportunityAsync(
        RangeBreakoutRetestStrategy strategy,
        ITradingDataProvider dataProvider,
        ITradingSignalAgent tradingSignalAgent,
        OpportunityRuntimeState state,
        DateOnly tradingDate,
        DateTimeOffset marketOpenUtc,
        IReadOnlyCollection<TradingPositionSnapshot> openPositions,
        IReadOnlyCollection<TradingOrderSnapshot> openOrders,
        ITradingTradePersistenceService tradePersistence,
        CancellationToken cancellationToken
    )
    {
        var evaluationStartedAtUtc = DateTimeOffset.UtcNow;
        _logger.LogInformation(
            "EvaluateOpportunity START Symbol={Symbol} Direction={Direction} Lifecycle={LifecycleState}.",
            state.Opportunity.Symbol,
            state.Opportunity.Direction,
            ResolveLifecycleState(state)
        );

        var options = _options.Value;
        if (HasExistingExposure(state.Opportunity.Symbol, openPositions, openOrders, out var linkedOrderId))
        {
            var wasMissingOrderId = string.IsNullOrWhiteSpace(state.OrderId);
            state.OrderPlaced = true;
            state.OrderId ??= linkedOrderId;
            state.OrderSubmittedAtUtc ??= DateTimeOffset.UtcNow;
            state.TradedInstrumentSymbol ??= state.Opportunity.Symbol;
            state.OrderSubmissionRejected = false;
            state.LastOrderSubmissionError = null;
            state.LastOrderSubmissionFailedAtUtc = null;
            state.PendingExitOrderId = null;
            state.PendingExitReason = null;
            state.EquityStopLossOrderId = null;
            state.EquityTrailingStopOrderId = null;
            state.PartialTakeProfitOrderId = null;
            state.LiveTrailingStopPrice = null;

            if (wasMissingOrderId && !string.IsNullOrWhiteSpace(linkedOrderId))
            {
                try
                {
                    var linkedOrder =
                        openOrders.FirstOrDefault(x =>
                            x.OrderId.Equals(linkedOrderId, StringComparison.OrdinalIgnoreCase)
                        ) ?? await dataProvider.GetOrderAsync(linkedOrderId, cancellationToken);
                    if (linkedOrder is not null)
                    {
                        state.TradedInstrumentSymbol = linkedOrder.Symbol;
                        await tradePersistence.RecordSubmittedAsync(
                            new TradingOrderSubmissionResult(
                                linkedOrder.OrderId,
                                linkedOrder.Symbol,
                                linkedOrder.Status,
                                linkedOrder.Side,
                                linkedOrder.Quantity
                            ),
                            new TradingTradeSubmissionSnapshot(
                                state.Opportunity.Symbol,
                                state.Opportunity.Direction,
                                linkedOrder.Quantity > 0m
                                    ? linkedOrder.Quantity
                                    : (
                                        options.UseOptionsTrading
                                            ? ResolveConfiguredOptionContracts(options.OrderQuantity)
                                            : ResolveConfiguredOrderQuantity(
                                                options.OrderQuantity,
                                                options.UseWholeShareQuantity
                                            )
                                    ),
                                state.PlannedEntryPrice ?? 0m,
                                state.StopLossPrice ?? 0m,
                                state.TakeProfitPrice ?? 0m,
                                0m,
                                state.Opportunity.Score,
                                0,
                                state.EntrySignalBarTimestampUtc,
                                state.Opportunity.SignalInsights,
                                state.OrderSubmittedAtUtc.Value,
                                state.OpeningRange?.Upper,
                                state.OpeningRange?.Lower,
                                BuildPersistedRetestAttempts(state),
                                SentimentAnalysisId: state.SentimentAnalysisId
                            ),
                            linkedOrder,
                            cancellationToken
                        );
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Failed to backfill existing open order trade for {Symbol} with order {OrderId}.",
                        state.Opportunity.Symbol,
                        linkedOrderId
                    );
                }
            }

            _logger.LogInformation(
                "Skipping signal execution for {Symbol} because an open position/order already exists.",
                state.Opportunity.Symbol
            );
            _logger.LogInformation(
                "EvaluateOpportunity END Symbol={Symbol} Result=HandledExistingExposure DurationMs={DurationMs}.",
                state.Opportunity.Symbol,
                (DateTimeOffset.UtcNow - evaluationStartedAtUtc).TotalMilliseconds
            );
            return true;
        }

        if (state.OrderSubmissionRejected)
        {
            _logger.LogDebug(
                "Skipping order submission retry for {Symbol} due to earlier non-retriable rejection: {Reason}.",
                state.Opportunity.Symbol,
                state.LastOrderSubmissionError
            );
            _logger.LogInformation(
                "EvaluateOpportunity END Symbol={Symbol} Result=SkipRejectedRetry DurationMs={DurationMs}.",
                state.Opportunity.Symbol,
                (DateTimeOffset.UtcNow - evaluationStartedAtUtc).TotalMilliseconds
            );
            return false;
        }

        var bars = await GetSessionBarsAsync(
            dataProvider,
            state.Opportunity.Symbol,
            marketOpenUtc,
            cancellationToken
        );
        state.LastSessionBars = bars;

        if (bars.Length < 6)
        {
            _logger.LogInformation(
                "EvaluateOpportunity END Symbol={Symbol} Result=InsufficientBars Bars={Bars} DurationMs={DurationMs}.",
                state.Opportunity.Symbol,
                bars.Length,
                (DateTimeOffset.UtcNow - evaluationStartedAtUtc).TotalMilliseconds
            );
            return false;
        }

        var thresholds = BuildStrategyThresholds(options);
        var openingRange = state.OpeningRange;
        if (openingRange is null)
        {
            if (!strategy.TryBuildOpeningRange(bars, marketOpenUtc, out var builtOpeningRange))
            {
                _logger.LogInformation(
                    "EvaluateOpportunity END Symbol={Symbol} Result=OpeningRangeNotReady DurationMs={DurationMs}.",
                    state.Opportunity.Symbol,
                    (DateTimeOffset.UtcNow - evaluationStartedAtUtc).TotalMilliseconds
                );
                return false;
            }

            openingRange = builtOpeningRange;
            state.OpeningRange = builtOpeningRange;
            _logger.LogInformation(
                "Opening range for {Symbol}: upper={Upper}, lower={Lower}, end={End}.",
                state.Opportunity.Symbol,
                builtOpeningRange!.Upper,
                builtOpeningRange.Lower,
                builtOpeningRange.EndTime
            );
        }

        if (openingRange is null)
        {
            _logger.LogInformation(
                "EvaluateOpportunity END Symbol={Symbol} Result=OpeningRangeUnavailable DurationMs={DurationMs}.",
                state.Opportunity.Symbol,
                (DateTimeOffset.UtcNow - evaluationStartedAtUtc).TotalMilliseconds
            );
            return false;
        }

        var adjustedOpeningRange = strategy.AdjustOpeningRangeForImmediateFailedBreakout(
            state.Opportunity.Direction,
            openingRange,
            bars,
            thresholds
        );
        if (!Equals(adjustedOpeningRange, openingRange))
        {
            openingRange = adjustedOpeningRange;
            state.OpeningRange = adjustedOpeningRange;
            state.BreakoutBar = null;
            state.LastEvaluatedRetestTimestamp = null;
            _logger.LogInformation(
                "Adjusted opening range for {Symbol} after immediate failed breakout: upper={Upper}, lower={Lower}, end={End}.",
                state.Opportunity.Symbol,
                adjustedOpeningRange.Upper,
                adjustedOpeningRange.Lower,
                adjustedOpeningRange.EndTime
            );
        }

        var breakoutStateReset = false;
        if (state.BreakoutBar is not null)
        {
            var invalidationBar = strategy.FindBreakoutInvalidationBar(
                state.Opportunity.Direction,
                openingRange,
                state.BreakoutBar.Timestamp,
                bars
            );
            if (invalidationBar is not null)
            {
                _logger.LogInformation(
                    "Resetting breakout state for {Symbol} because price moved back into range at {Timestamp}. PreviousBreakout={BreakoutTimestamp}.",
                    state.Opportunity.Symbol,
                    invalidationBar.Timestamp,
                    state.BreakoutBar.Timestamp
                );
                state.BreakoutBar = null;
                state.LastEvaluatedRetestTimestamp = invalidationBar.Timestamp;
                breakoutStateReset = true;
            }
        }

        if (state.BreakoutBar is null)
        {
            var breakoutBar = strategy.FindBreakoutBar(
                state.Opportunity.Direction,
                openingRange,
                bars,
                state.LastEvaluatedRetestTimestamp,
                thresholds
            );
            if (breakoutBar is null)
            {
                _logger.LogInformation(
                    "EvaluateOpportunity END Symbol={Symbol} Result=AwaitingBreakout DurationMs={DurationMs}.",
                    state.Opportunity.Symbol,
                    (DateTimeOffset.UtcNow - evaluationStartedAtUtc).TotalMilliseconds
                );
                return breakoutStateReset;
            }

            state.BreakoutBar = breakoutBar;
            state.LastEvaluatedRetestTimestamp = null;
            _logger.LogInformation(
                "Breakout detected for {Symbol} at {Timestamp} in {Direction} direction.",
                state.Opportunity.Symbol,
                breakoutBar.Timestamp,
                state.Opportunity.Direction
            );
        }

        TradingBarSnapshot? retestBar;
        RetestVerificationResult? retestValidation;
        var rejectedRetestCount = 0;
        while (true)
        {
            retestBar = strategy.FindRetestBar(
                state.Opportunity.Direction,
                openingRange,
                state.BreakoutBar.Timestamp,
                state.LastEvaluatedRetestTimestamp,
                bars,
                thresholds
            );

            if (retestBar is null)
            {
                _logger.LogInformation(
                    "EvaluateOpportunity END Symbol={Symbol} Result=AwaitingRetest RejectedRetests={RejectedRetests} DurationMs={DurationMs}.",
                    state.Opportunity.Symbol,
                    rejectedRetestCount,
                    (DateTimeOffset.UtcNow - evaluationStartedAtUtc).TotalMilliseconds
                );
                return breakoutStateReset || rejectedRetestCount > 0;
            }

            state.LastEvaluatedRetestTimestamp = retestBar.Timestamp;
            if (
                !strategy.MeetsEntryExecutionConstraints(
                    state.Opportunity.Direction,
                    openingRange,
                    state.BreakoutBar,
                    retestBar,
                    marketOpenUtc,
                    options.MinimumMinutesFromMarketOpenForEntry,
                    options.MinimumEntryDistanceFromRangeFraction,
                    out var entryConstraintRejectionReason,
                    options.MaximumMinutesFromMarketOpenForEntry
                )
            )
            {
                rejectedRetestCount++;
                state.RetestAttempts.Add(
                    new RetestAttemptRuntimeState(
                        Guid.NewGuid().ToString("N")[..8],
                        retestBar,
                        false,
                        0,
                        entryConstraintRejectionReason,
                        null
                    )
                );

                _logger.LogInformation(
                    "Retest rejected by deterministic entry constraints for {Symbol}: {Payload}",
                    state.Opportunity.Symbol,
                    JsonSerializer.Serialize(
                        new
                        {
                            Symbol = state.Opportunity.Symbol,
                            Direction = state.Opportunity.Direction.ToString(),
                            RetestTimestamp = retestBar.Timestamp,
                            RejectionReason = entryConstraintRejectionReason,
                        }
                    )
                );

                continue;
            }

            if (options.UseRetestValidationAgent)
            {
                retestValidation = await tradingSignalAgent.VerifyRetestAsync(
                    new RetestVerificationRequest(
                        state.Opportunity.Symbol,
                        state.Opportunity.Direction,
                        openingRange.Upper,
                        openingRange.Lower,
                        state.BreakoutBar,
                        retestBar,
                        bars
                    ),
                    tradingDate,
                    cancellationToken
                );
            }
            else
            {
                retestValidation = BuildStrategyOnlyRetestValidation(
                    state.Opportunity.Symbol,
                    state.Opportunity.Direction,
                    retestBar
                );
            }

            var acceptedRetest = IsAcceptedRetestValidation(
                state.Opportunity.Direction,
                retestValidation,
                options.MinimumRetestScore
            );
            var rejectionReason = acceptedRetest
                ? null
                : retestValidation?.InvalidationReason
                    ?? retestValidation?.Reason
                    ?? "Retest validation did not meet strategy thresholds.";

            state.RetestAttempts.Add(
                new RetestAttemptRuntimeState(
                    Guid.NewGuid().ToString("N")[..8],
                    retestBar,
                    acceptedRetest,
                    retestValidation?.Score ?? 0,
                    rejectionReason,
                    retestValidation
                )
            );

            if (acceptedRetest)
            {
                break;
            }

            rejectedRetestCount++;
            _logger.LogInformation(
                "Retest validation rejected for {Symbol}: {Payload}",
                state.Opportunity.Symbol,
                JsonSerializer.Serialize(
                    new
                    {
                        Symbol = state.Opportunity.Symbol,
                        Direction = state.Opportunity.Direction.ToString(),
                        Score = retestValidation?.Score ?? 0,
                        RetestTimestamp = retestBar.Timestamp,
                        RejectionReason = rejectionReason,
                    }
                )
            );
        }

        if (retestBar is null || retestValidation is null)
        {
            _logger.LogInformation(
                "EvaluateOpportunity END Symbol={Symbol} Result=RetestResolutionUnavailable DurationMs={DurationMs}.",
                state.Opportunity.Symbol,
                (DateTimeOffset.UtcNow - evaluationStartedAtUtc).TotalMilliseconds
            );
            return breakoutStateReset || rejectedRetestCount > 0;
        }

        var acceptedRetestScore = retestValidation.Score;

        var quote = _streamingCache.TryGetQuote(state.Opportunity.Symbol, out var streamQuote)
            ? streamQuote
            : await dataProvider.GetQuoteAsync(state.Opportunity.Symbol, cancellationToken);
        var entryPrice = quote.LastPrice > 0m ? quote.LastPrice : retestBar.Close;
        var tradePlan = strategy.BuildTradePlan(
            state.Opportunity.Direction,
            entryPrice,
            retestBar,
            options.StopLossBufferFraction,
            options.RewardToRiskRatio
        );

        if (tradePlan is null)
        {
            _logger.LogWarning(
                "Trade plan could not be created for {Symbol}. Entry={EntryPrice}.",
                state.Opportunity.Symbol,
                entryPrice
            );
            _logger.LogInformation(
                "EvaluateOpportunity END Symbol={Symbol} Result=TradePlanUnavailable DurationMs={DurationMs}.",
                state.Opportunity.Symbol,
                (DateTimeOffset.UtcNow - evaluationStartedAtUtc).TotalMilliseconds
            );
            return true;
        }

        var referenceEntryPrice = tradePlan.EntryPrice > 0m ? tradePlan.EntryPrice : entryPrice;
        var effectiveUnderlyingTradePlan = EnsureMinimumUnderlyingTradePlan(
            state.Opportunity.Direction,
            tradePlan,
            referenceEntryPrice,
            options.StopLossBufferFraction,
            options.RewardToRiskRatio
        );
        TradingOptionContractSnapshot? selectedOptionContract = null;
        TradePlan? optionTradePlan = null;
        decimal orderQuantity;
        decimal optionPlannedEntryPrice = 0m;
        var plannedRiskPerUnit = options.UseOptionsTrading ? 0m : effectiveUnderlyingTradePlan.RiskPerUnit;
        var useLiveTrailingStopLoss = !options.UseOptionsTrading && options.LiveUseTrailingStopLoss;
        TradingOrderSubmissionResult order;

        try
        {
            if (options.UseOptionsTrading)
            {
                selectedOptionContract = await dataProvider.SelectOptionContractAsync(
                    state.Opportunity.Symbol,
                    state.Opportunity.Direction,
                    referenceEntryPrice,
                    tradingDate,
                    options.OptionMinDaysToExpiration,
                    options.OptionMaxDaysToExpiration,
                    cancellationToken
                );

                if (selectedOptionContract is null)
                {
                    _logger.LogWarning(
                        "Skipping order for {Symbol}: no eligible option contract found for direction {Direction}.",
                        state.Opportunity.Symbol,
                        state.Opportunity.Direction
                    );
                    _logger.LogInformation(
                        "EvaluateOpportunity END Symbol={Symbol} Result=NoOptionContract DurationMs={DurationMs}.",
                        state.Opportunity.Symbol,
                        (DateTimeOffset.UtcNow - evaluationStartedAtUtc).TotalMilliseconds
                    );
                    return true;
                }

                TradingOptionQuoteSnapshot? optionQuote = null;
                try
                {
                    optionQuote = await dataProvider.GetOptionQuoteAsync(
                        selectedOptionContract.Symbol,
                        cancellationToken
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Failed to fetch option quote for {OptionSymbol}. Falling back to latest contract close price.",
                        selectedOptionContract.Symbol
                    );
                }

                var optionPremium = ResolveOptionPremium(optionQuote, selectedOptionContract.ClosePrice);
                orderQuantity = ResolveConfiguredOptionContracts(options.OrderQuantity);
                if (orderQuantity <= 0m)
                {
                    _logger.LogWarning(
                        "Skipping option order for {Symbol}: invalid contract quantity configuration. OrderQuantity={OrderQuantity}.",
                        state.Opportunity.Symbol,
                        options.OrderQuantity
                    );
                    _logger.LogInformation(
                        "EvaluateOpportunity END Symbol={Symbol} Result=InvalidOptionQuantity DurationMs={DurationMs}.",
                        state.Opportunity.Symbol,
                        (DateTimeOffset.UtcNow - evaluationStartedAtUtc).TotalMilliseconds
                    );
                    return true;
                }

                optionPlannedEntryPrice = optionPremium > 0m ? optionPremium : 0m;
                optionTradePlan = BuildOptionTradePlan(
                    optionPlannedEntryPrice,
                    effectiveUnderlyingTradePlan,
                    referenceEntryPrice,
                    options.RewardToRiskRatio
                );

                order = await dataProvider.SubmitOptionOrderAsync(
                    new TradingOptionOrderRequest(
                        selectedOptionContract.Symbol,
                        TradingOrderSide.Buy,
                        (int)orderQuantity
                    ),
                    cancellationToken
                );
            }
            else
            {
                orderQuantity = ResolveConfiguredOrderQuantity(
                    options.OrderQuantity,
                    options.UseWholeShareQuantity
                );
                if (orderQuantity <= 0m)
                {
                    _logger.LogWarning(
                        "Skipping order for {Symbol}: invalid fixed quantity sizing. OrderQuantity={OrderQuantity}, UseWholeShareQuantity={UseWholeShareQuantity}.",
                        state.Opportunity.Symbol,
                        options.OrderQuantity,
                        options.UseWholeShareQuantity
                    );
                    _logger.LogInformation(
                        "EvaluateOpportunity END Symbol={Symbol} Result=InvalidShareQuantity DurationMs={DurationMs}.",
                        state.Opportunity.Symbol,
                        (DateTimeOffset.UtcNow - evaluationStartedAtUtc).TotalMilliseconds
                    );
                    return true;
                }

                if (useLiveTrailingStopLoss)
                {
                    order = await dataProvider.SubmitMarketOrderAsync(
                        new TradingMarketOrderRequest(
                            state.Opportunity.Symbol,
                            state.Opportunity.Direction == TradingDirection.Bullish
                                ? TradingOrderSide.Buy
                                : TradingOrderSide.Sell,
                            orderQuantity
                        ),
                        cancellationToken
                    );
                }
                else
                {
                    order = await dataProvider.SubmitBracketOrderAsync(
                        new TradingBracketOrderRequest(
                            state.Opportunity.Symbol,
                            state.Opportunity.Direction,
                            orderQuantity,
                            effectiveUnderlyingTradePlan.StopLossPrice,
                            effectiveUnderlyingTradePlan.TakeProfitPrice
                        ),
                        cancellationToken
                    );
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation(
                "EvaluateOpportunity END Symbol={Symbol} Result=Canceled DurationMs={DurationMs}.",
                state.Opportunity.Symbol,
                (DateTimeOffset.UtcNow - evaluationStartedAtUtc).TotalMilliseconds
            );
            throw;
        }
        catch (AlpacaApiException ex) when (ShouldSuppressOrderSubmissionRetry(ex))
        {
            state.OrderSubmissionRejected = true;
            state.LastOrderSubmissionError = BuildSubmissionErrorSummary(ex);
            state.LastOrderSubmissionFailedAtUtc = DateTimeOffset.UtcNow;

            _logger.LogWarning(
                ex,
                "Alpaca rejected order submission for {Symbol}. Suppressing retries until next trading day. StatusCode={StatusCode}, AlpacaCode={AlpacaCode}, AlpacaMessage={AlpacaMessage}, RequestId={RequestId}.",
                state.Opportunity.Symbol,
                ex.StatusCode,
                ex.AlpacaCode,
                ex.AlpacaMessage,
                ex.RequestId
            );

            _logger.LogInformation(
                "EvaluateOpportunity END Symbol={Symbol} Result=NonRetriableOrderRejection DurationMs={DurationMs}.",
                state.Opportunity.Symbol,
                (DateTimeOffset.UtcNow - evaluationStartedAtUtc).TotalMilliseconds
            );
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Order submission failed for {Symbol}; will retry in a future tick.",
                state.Opportunity.Symbol
            );
            _logger.LogInformation(
                "EvaluateOpportunity END Symbol={Symbol} Result=RetriableOrderSubmissionFailure DurationMs={DurationMs}.",
                state.Opportunity.Symbol,
                (DateTimeOffset.UtcNow - evaluationStartedAtUtc).TotalMilliseconds
            );
            return false;
        }

        state.OrderPlaced = true;
        state.OrderId = order.OrderId;
        state.OrderSubmittedAtUtc = DateTimeOffset.UtcNow;
        state.EntrySignalBarTimestampUtc = retestBar.Timestamp;
        state.PlannedEntryPrice = referenceEntryPrice;
        state.StopLossPrice = effectiveUnderlyingTradePlan.StopLossPrice;
        state.TakeProfitPrice = effectiveUnderlyingTradePlan.TakeProfitPrice;
        state.OrderSubmissionRejected = false;
        state.LastOrderSubmissionError = null;
        state.LastOrderSubmissionFailedAtUtc = null;
        state.TradedInstrumentSymbol = order.Symbol;
        state.OptionContractType = selectedOptionContract?.ContractType.ToString();
        state.OptionStrikePrice = selectedOptionContract?.StrikePrice;
        state.OptionExpirationDate = selectedOptionContract?.ExpirationDate;
        state.PendingExitOrderId = null;
        state.PendingExitReason = null;
        state.OptionStopLossPrice = optionTradePlan?.StopLossPrice;
        state.OptionTakeProfitPrice = optionTradePlan?.TakeProfitPrice;
        state.OptionStopLossOrderId = null;
        state.OptionTakeProfitOrderId = null;
        state.UseTrailingStopLoss = useLiveTrailingStopLoss;
        state.InitialRiskPerUnit = useLiveTrailingStopLoss ? effectiveUnderlyingTradePlan.RiskPerUnit : null;
        var liveSplit = ResolveLivePositionSplit(
            orderQuantity,
            options.LivePartialTakeProfitFraction,
            options.UseWholeShareQuantity
        );
        state.PlannedPartialTakeProfitQuantity =
            useLiveTrailingStopLoss && liveSplit.IsEnabled ? liveSplit.PartialTakeProfitQuantity : null;
        state.PlannedRunnerQuantity =
            useLiveTrailingStopLoss && liveSplit.IsEnabled ? liveSplit.RunnerQuantity : null;
        state.RemainingRunnerQuantity =
            useLiveTrailingStopLoss && liveSplit.IsEnabled ? liveSplit.RunnerQuantity : null;
        state.EquityStopLossOrderId = null;
        state.EquityTrailingStopOrderId = null;
        state.PartialTakeProfitOrderId = null;
        state.PartialTakeProfitFilled = false;
        state.PartialTakeProfitFilledAtUtc = null;
        state.LiveTrailingStopPrice = null;
        state.LiveTrailingStopActivatedAtUtc = null;

        TradingOrderSnapshot? submittedOrderSnapshot = null;
        try
        {
            submittedOrderSnapshot = await dataProvider.GetOrderAsync(order.OrderId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to fetch submitted Alpaca order snapshot for {Symbol} with order {OrderId}.",
                state.Opportunity.Symbol,
                order.OrderId
            );
        }

        try
        {
            await tradePersistence.RecordSubmittedAsync(
                order,
                new TradingTradeSubmissionSnapshot(
                    state.Opportunity.Symbol,
                    state.Opportunity.Direction,
                    orderQuantity,
                    referenceEntryPrice,
                    effectiveUnderlyingTradePlan.StopLossPrice,
                    effectiveUnderlyingTradePlan.TakeProfitPrice,
                    effectiveUnderlyingTradePlan.RiskPerUnit,
                    state.Opportunity.Score,
                    acceptedRetestScore,
                    retestBar.Timestamp,
                    state.Opportunity.SignalInsights,
                    state.OrderSubmittedAtUtc.Value,
                    state.OpeningRange?.Upper,
                    state.OpeningRange?.Lower,
                    BuildPersistedRetestAttempts(state),
                    optionTradePlan?.EntryPrice,
                    optionTradePlan?.StopLossPrice,
                    optionTradePlan?.TakeProfitPrice,
                    optionTradePlan?.RiskPerUnit,
                    state.SentimentAnalysisId
                ),
                submittedOrderSnapshot,
                cancellationToken
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to persist submitted trade for {Symbol} with order {OrderId}.",
                state.Opportunity.Symbol,
                order.OrderId
            );
        }

        _logger.LogInformation(
            "Order placed for {Symbol}: {Payload}",
            state.Opportunity.Symbol,
            JsonSerializer.Serialize(
                new
                {
                    Symbol = state.Opportunity.Symbol,
                    Direction = state.Opportunity.Direction.ToString(),
                    RetestScore = acceptedRetestScore,
                    state.Opportunity.SignalInsights,
                    UnderlyingEntryPrice = referenceEntryPrice,
                    UnderlyingStopLoss = effectiveUnderlyingTradePlan.StopLossPrice,
                    UnderlyingTakeProfit = effectiveUnderlyingTradePlan.TakeProfitPrice,
                    OptionEntryPrice = optionTradePlan?.EntryPrice,
                    OptionStopLoss = optionTradePlan?.StopLossPrice,
                    OptionTakeProfit = optionTradePlan?.TakeProfitPrice,
                    Quantity = orderQuantity,
                    InstrumentSymbol = order.Symbol,
                    SignalRetestBarTimestampUtc = retestBar.Timestamp,
                    OrderId = order.OrderId,
                    OrderSubmittedAtUtc = state.OrderSubmittedAtUtc,
                }
            )
        );
        _logger.LogInformation(
            "EvaluateOpportunity END Symbol={Symbol} Result=OrderPlaced DurationMs={DurationMs}.",
            state.Opportunity.Symbol,
            (DateTimeOffset.UtcNow - evaluationStartedAtUtc).TotalMilliseconds
        );
        return true;
    }

    private async Task<bool> AuditActiveOrdersAsync(
        ITradingDataProvider dataProvider,
        ITradingTradePersistenceService tradePersistence,
        DateTimeOffset marketOpenUtc,
        CancellationToken cancellationToken
    )
    {
        var pendingAuditStates = _watchStates
            .Values.Where(x => x.OrderPlaced && (!x.EntryAuditLogged || !x.ExitAuditLogged))
            .ToArray();
        if (pendingAuditStates.Length == 0)
        {
            return false;
        }

        var stateChanged = false;
        foreach (var state in pendingAuditStates)
        {
            try
            {
                stateChanged =
                    await AuditOrderLifecycleAsync(
                        dataProvider,
                        tradePersistence,
                        state,
                        marketOpenUtc,
                        cancellationToken
                    )
                    || stateChanged;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to audit order lifecycle for {Symbol} with order {OrderId}.",
                    state.Opportunity.Symbol,
                    state.OrderId
                );
            }
        }

        return stateChanged;
    }

    private async Task<bool> AuditOrderLifecycleAsync(
        ITradingDataProvider dataProvider,
        ITradingTradePersistenceService tradePersistence,
        OpportunityRuntimeState state,
        DateTimeOffset marketOpenUtc,
        CancellationToken cancellationToken
    )
    {
        var auditStartedAtUtc = DateTimeOffset.UtcNow;
        _logger.LogInformation(
            "AuditOrderLifecycle START Symbol={Symbol} OrderId={OrderId} EntryAudited={EntryAudited} ExitAudited={ExitAudited}.",
            state.Opportunity.Symbol,
            state.OrderId,
            state.EntryAuditLogged,
            state.ExitAuditLogged
        );

        if (string.IsNullOrWhiteSpace(state.OrderId))
        {
            _logger.LogInformation(
                "AuditOrderLifecycle END Symbol={Symbol} Result=MissingOrderId DurationMs={DurationMs}.",
                state.Opportunity.Symbol,
                (DateTimeOffset.UtcNow - auditStartedAtUtc).TotalMilliseconds
            );
            return false;
        }

        var order = _streamingCache.TryGetOrder(state.OrderId, out var streamOrder)
            ? streamOrder
            : await dataProvider.GetOrderAsync(state.OrderId, cancellationToken);
        if (order is null)
        {
            _logger.LogInformation(
                "AuditOrderLifecycle END Symbol={Symbol} Result=OrderNotFound DurationMs={DurationMs}.",
                state.Opportunity.Symbol,
                (DateTimeOffset.UtcNow - auditStartedAtUtc).TotalMilliseconds
            );
            return false;
        }

        var stateChanged = false;
        if (!state.EntryAuditLogged && order.FilledAt is DateTimeOffset entryFilledAtUtc)
        {
            state.EntryAuditLogged = true;
            state.EntryFilledAtUtc = entryFilledAtUtc;
            stateChanged = true;
            if (state.UseTrailingStopLoss)
            {
                stateChanged =
                    TryRebaseUnderlyingRiskLevelsFromEntryFill(
                        state,
                        order.FilledAveragePrice,
                        _options.Value.RewardToRiskRatio
                    )
                    || stateChanged;
            }

            try
            {
                await tradePersistence.RecordEntryFillAsync(state.OrderId!, order, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to persist entry fill for {Symbol} with order {OrderId}.",
                    state.Opportunity.Symbol,
                    state.OrderId
                );
            }

            var entryBarContext = await BuildLiveTradeBarContextAsync(
                dataProvider,
                state.Opportunity.Symbol,
                marketOpenUtc,
                entryFilledAtUtc,
                cancellationToken
            );
            state.EntryBarTimestampUtc = entryBarContext?.BarTimestampUtc;
            state.EntryBarIndex = entryBarContext?.BarIndex;

            _logger.LogInformation(
                "Live trade entry audit for {Symbol}: {Payload}",
                state.Opportunity.Symbol,
                JsonSerializer.Serialize(
                    new
                    {
                        Symbol = state.Opportunity.Symbol,
                        Direction = state.Opportunity.Direction.ToString(),
                        OrderId = state.OrderId,
                        PlannedSignalBarTimestampUtc = state.EntrySignalBarTimestampUtc,
                        OrderSubmittedAtUtc = state.OrderSubmittedAtUtc,
                        EntryFilledAtUtc = entryFilledAtUtc,
                        EntryBarTimestampUtc = state.EntryBarTimestampUtc,
                        EntryBarIndex = state.EntryBarIndex,
                        PlannedEntryPrice = state.PlannedEntryPrice,
                        FilledAveragePrice = order.FilledAveragePrice,
                        StopLoss = state.StopLossPrice,
                        TakeProfit = state.TakeProfitPrice,
                    }
                )
            );

            if (LooksLikeOptionContractSymbol(order.Symbol))
            {
                stateChanged =
                    await TryPlaceOptionExitBracketAsync(dataProvider, state, order, cancellationToken)
                    || stateChanged;
            }
        }

        if (state.ExitAuditLogged)
        {
            _logger.LogInformation(
                "AuditOrderLifecycle END Symbol={Symbol} Result=AlreadyExited StateChanged={StateChanged} DurationMs={DurationMs}.",
                state.Opportunity.Symbol,
                stateChanged,
                (DateTimeOffset.UtcNow - auditStartedAtUtc).TotalMilliseconds
            );
            return stateChanged;
        }

        if (!string.IsNullOrWhiteSpace(state.PendingExitOrderId))
        {
            stateChanged =
                await TryFinalizePendingExitOrderAsync(
                    dataProvider,
                    tradePersistence,
                    state,
                    order,
                    marketOpenUtc,
                    cancellationToken
                )
                || stateChanged;

            if (state.ExitAuditLogged)
            {
                _logger.LogInformation(
                    "AuditOrderLifecycle END Symbol={Symbol} Result=PendingExitFinalized StateChanged={StateChanged} DurationMs={DurationMs}.",
                    state.Opportunity.Symbol,
                    stateChanged,
                    (DateTimeOffset.UtcNow - auditStartedAtUtc).TotalMilliseconds
                );
                return stateChanged;
            }
        }

        if (LooksLikeOptionContractSymbol(order.Symbol))
        {
            stateChanged =
                await TryFinalizeOptionExitBracketAsync(
                    dataProvider,
                    tradePersistence,
                    state,
                    order,
                    marketOpenUtc,
                    cancellationToken
                )
                || stateChanged;

            _logger.LogInformation(
                "AuditOrderLifecycle END Symbol={Symbol} Result=OptionExitAuditCompleted StateChanged={StateChanged} ExitAudited={ExitAudited} DurationMs={DurationMs}.",
                state.Opportunity.Symbol,
                stateChanged,
                state.ExitAuditLogged,
                (DateTimeOffset.UtcNow - auditStartedAtUtc).TotalMilliseconds
            );
            return stateChanged;
        }

        if (state.UseTrailingStopLoss)
        {
            stateChanged =
                await TryManageEquityTrailingStopLifecycleAsync(
                    dataProvider,
                    tradePersistence,
                    state,
                    order,
                    marketOpenUtc,
                    cancellationToken
                )
                || stateChanged;

            if (state.ExitAuditLogged)
            {
                _logger.LogInformation(
                    "AuditOrderLifecycle END Symbol={Symbol} Result=EquityTrailingStopManaged StateChanged={StateChanged} DurationMs={DurationMs}.",
                    state.Opportunity.Symbol,
                    stateChanged,
                    (DateTimeOffset.UtcNow - auditStartedAtUtc).TotalMilliseconds
                );
                return stateChanged;
            }
        }

        var exitLeg = order.Legs
            .Where(x => x.FilledAt is not null)
            .OrderByDescending(x => x.FilledAt)
            .FirstOrDefault();
        if (exitLeg is null)
        {
            _logger.LogInformation(
                "AuditOrderLifecycle END Symbol={Symbol} Result=AwaitingExitFill StateChanged={StateChanged} DurationMs={DurationMs}.",
                state.Opportunity.Symbol,
                stateChanged,
                (DateTimeOffset.UtcNow - auditStartedAtUtc).TotalMilliseconds
            );
            return stateChanged;
        }

        var exitFilledAtUtc = exitLeg.FilledAt!.Value;
        state.ExitAuditLogged = true;
        state.ExitFilledAtUtc = exitFilledAtUtc;
        state.PendingExitOrderId = null;
        state.PendingExitReason = null;
        stateChanged = true;

        var exitReason = DetermineExitReasonFromOrderType(exitLeg.OrderType);
        var feeActivities = await LoadFeeActivitiesSafeAsync(dataProvider, cancellationToken);
        var estimatedSpreadBps = Math.Max(0m, _options.Value.BacktestEstimatedSpreadBps);
        try
        {
            await tradePersistence.RecordExitFillAsync(
                state.OrderId!,
                order,
                exitLeg,
                exitReason,
                feeActivities,
                estimatedSpreadBps,
                DateTimeOffset.UtcNow,
                cancellationToken
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to persist exit fill for {Symbol} with order {OrderId}.",
                state.Opportunity.Symbol,
                state.OrderId
            );
        }

        var exitBarContext = await BuildLiveTradeBarContextAsync(
            dataProvider,
            state.Opportunity.Symbol,
            marketOpenUtc,
            exitFilledAtUtc,
            cancellationToken
        );
        state.ExitBarTimestampUtc = exitBarContext?.BarTimestampUtc;
        state.ExitBarIndex = exitBarContext?.BarIndex;

        var entryFilledAt = state.EntryFilledAtUtc ?? order.FilledAt ?? state.OrderSubmittedAtUtc ?? exitFilledAtUtc;
        var openDuration = exitFilledAtUtc - entryFilledAt;
        if (openDuration < TimeSpan.Zero)
        {
            openDuration = TimeSpan.Zero;
        }

        int? barsOpen = null;
        if (state.EntryBarIndex is int entryBarIndex && state.ExitBarIndex is int exitBarIndex)
        {
            barsOpen = Math.Max(0, exitBarIndex - entryBarIndex);
        }

        _logger.LogInformation(
            "Live trade close audit for {Symbol}: {Payload}",
            state.Opportunity.Symbol,
            JsonSerializer.Serialize(
                new
                {
                    Symbol = state.Opportunity.Symbol,
                    Direction = state.Opportunity.Direction.ToString(),
                    OrderId = state.OrderId,
                    EntryFilledAtUtc = entryFilledAt,
                    EntryBarTimestampUtc = state.EntryBarTimestampUtc,
                    EntryBarIndex = state.EntryBarIndex,
                    ExitFilledAtUtc = exitFilledAtUtc,
                    ExitBarTimestampUtc = state.ExitBarTimestampUtc,
                    ExitBarIndex = state.ExitBarIndex,
                    BarsOpen = barsOpen,
                    OpenDurationMinutes = decimal.Round((decimal)openDuration.TotalMinutes, 2),
                    ExitReason = exitReason,
                    ExitOrderType = exitLeg.OrderType,
                    StopLoss = state.StopLossPrice,
                    TakeProfit = state.TakeProfitPrice,
                }
            )
        );

        _logger.LogInformation(
            "AuditOrderLifecycle END Symbol={Symbol} Result=ExitAudited StateChanged={StateChanged} DurationMs={DurationMs}.",
            state.Opportunity.Symbol,
            stateChanged,
            (DateTimeOffset.UtcNow - auditStartedAtUtc).TotalMilliseconds
        );
        return stateChanged;
    }

    private async Task<bool> TryManageEquityTrailingStopLifecycleAsync(
        ITradingDataProvider dataProvider,
        ITradingTradePersistenceService tradePersistence,
        OpportunityRuntimeState state,
        TradingOrderSnapshot parentOrder,
        DateTimeOffset marketOpenUtc,
        CancellationToken cancellationToken
    )
    {
        if (!state.UseTrailingStopLoss || state.ExitAuditLogged || state.EntryFilledAtUtc is null)
        {
            return false;
        }

        if (state.StopLossPrice is not decimal stopLossPrice
            || state.TakeProfitPrice is not decimal takeProfitPrice
            || stopLossPrice <= 0m
            || takeProfitPrice <= 0m)
        {
            return false;
        }

        var options = _options.Value;
        var stateChanged = false;

        var openPositionQuantity = await GetOpenUnderlyingPositionQuantityAsync(
            dataProvider,
            state.Opportunity.Symbol,
            cancellationToken
        );

        if (openPositionQuantity <= 0m)
        {
            if (await TryFinalizeEquityExitOrderIfFilledAsync(
                    dataProvider,
                    tradePersistence,
                    state,
                    parentOrder,
                    state.EquityTrailingStopOrderId,
                    "TrailingStopAfterTakeProfit",
                    marketOpenUtc,
                    cancellationToken
                ))
            {
                return true;
            }

            if (await TryFinalizeEquityExitOrderIfFilledAsync(
                    dataProvider,
                    tradePersistence,
                    state,
                    parentOrder,
                    state.EquityStopLossOrderId,
                    "StopLoss",
                    marketOpenUtc,
                    cancellationToken
                ))
            {
                return true;
            }

            if (await TryFinalizeEquityExitOrderIfFilledAsync(
                    dataProvider,
                    tradePersistence,
                    state,
                    parentOrder,
                    state.PartialTakeProfitOrderId,
                    "TakeProfit",
                    marketOpenUtc,
                    cancellationToken
                ))
            {
                return true;
            }

            return false;
        }

        if (state.PlannedPartialTakeProfitQuantity is null || state.PlannedRunnerQuantity is null)
        {
            var split = ResolveLivePositionSplit(
                openPositionQuantity,
                options.LivePartialTakeProfitFraction,
                options.UseWholeShareQuantity
            );
            if (split.IsEnabled)
            {
                state.PlannedPartialTakeProfitQuantity = split.PartialTakeProfitQuantity;
                state.PlannedRunnerQuantity = split.RunnerQuantity;
                state.RemainingRunnerQuantity = split.RunnerQuantity;
            }
            else
            {
                state.PlannedPartialTakeProfitQuantity = openPositionQuantity;
                state.PlannedRunnerQuantity = 0m;
                state.RemainingRunnerQuantity = 0m;
            }
            stateChanged = true;
        }

        var exitSide = ResolveExitSideForDirection(state.Opportunity.Direction);
        if (!state.PartialTakeProfitFilled)
        {
            stateChanged =
                await EnsureEquityStopLossOrderAsync(
                    dataProvider,
                    state,
                    exitSide,
                    openPositionQuantity,
                    stopLossPrice,
                    cancellationToken
                )
                || stateChanged;

            if (await TryFinalizeEquityExitOrderIfFilledAsync(
                    dataProvider,
                    tradePersistence,
                    state,
                    parentOrder,
                    state.EquityStopLossOrderId,
                    "StopLoss",
                    marketOpenUtc,
                    cancellationToken
                ))
            {
                return true;
            }

            stateChanged =
                await TryFinalizePartialTakeProfitOrderAsync(
                    dataProvider,
                    state,
                    openPositionQuantity,
                    cancellationToken
                )
                || stateChanged;

            if (!state.PartialTakeProfitFilled && string.IsNullOrWhiteSpace(state.PartialTakeProfitOrderId))
            {
                var quote = _streamingCache.TryGetQuote(state.Opportunity.Symbol, out var streamQuote)
                    ? streamQuote
                    : await dataProvider.GetQuoteAsync(state.Opportunity.Symbol, cancellationToken);
                var referencePrice = ResolveUnderlyingReferencePrice(quote);
                var takeProfitTriggered = IsTakeProfitTriggered(
                    state.Opportunity.Direction,
                    referencePrice,
                    takeProfitPrice
                );

                if (takeProfitTriggered)
                {
                    var partialQuantity = ResolvePartialTakeProfitQuantityForLive(
                        state,
                        openPositionQuantity,
                        options.UseWholeShareQuantity
                    );

                    if (partialQuantity > 0m)
                    {
                        if (!string.IsNullOrWhiteSpace(state.EquityStopLossOrderId))
                        {
                            try
                            {
                                await dataProvider.CancelOrderAsync(state.EquityStopLossOrderId, cancellationToken);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(
                                    ex,
                                    "Failed to cancel equity stop-loss order {OrderId} for {Symbol} before partial take-profit.",
                                    state.EquityStopLossOrderId,
                                    state.Opportunity.Symbol
                                );
                            }
                            state.EquityStopLossOrderId = null;
                            stateChanged = true;
                        }

                        var partialOrder = await dataProvider.SubmitMarketOrderAsync(
                            new TradingMarketOrderRequest(
                                state.Opportunity.Symbol,
                                exitSide,
                                partialQuantity
                            ),
                            cancellationToken
                        );
                        state.PartialTakeProfitOrderId = partialOrder.OrderId;
                        stateChanged = true;
                    }
                }
            }

            return stateChanged;
        }

        stateChanged =
            await EnsureEquityTrailingStopOrderAsync(
                dataProvider,
                state,
                exitSide,
                openPositionQuantity,
                cancellationToken
            )
            || stateChanged;

        if (await TryFinalizeEquityExitOrderIfFilledAsync(
                dataProvider,
                tradePersistence,
                state,
                parentOrder,
                state.EquityTrailingStopOrderId,
                "TrailingStopAfterTakeProfit",
                marketOpenUtc,
                cancellationToken
            ))
        {
            return true;
        }

        return stateChanged;
    }

    private async Task<bool> EnsureEquityStopLossOrderAsync(
        ITradingDataProvider dataProvider,
        OpportunityRuntimeState state,
        TradingOrderSide exitSide,
        decimal openPositionQuantity,
        decimal stopLossPrice,
        CancellationToken cancellationToken
    )
    {
        if (!string.IsNullOrWhiteSpace(state.EquityStopLossOrderId))
        {
            return false;
        }

        var quantity = ResolveOrderQuantityForLive(openPositionQuantity, _options.Value.UseWholeShareQuantity);
        if (quantity <= 0m)
        {
            return false;
        }

        try
        {
            var order = await dataProvider.SubmitEquityStopLossOrderAsync(
                new TradingEquityStopLossOrderRequest(
                    state.Opportunity.Symbol,
                    exitSide,
                    quantity,
                    stopLossPrice
                ),
                cancellationToken
            );
            state.EquityStopLossOrderId = order.OrderId;
            _logger.LogInformation(
                "Equity protective stop-loss order placed for {Symbol}: OrderId={OrderId}, StopPrice={StopPrice}, Quantity={Quantity}.",
                state.Opportunity.Symbol,
                order.OrderId,
                stopLossPrice,
                quantity
            );
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to place equity stop-loss order for {Symbol}.",
                state.Opportunity.Symbol
            );
            return false;
        }
    }

    private async Task<bool> TryFinalizePartialTakeProfitOrderAsync(
        ITradingDataProvider dataProvider,
        OpportunityRuntimeState state,
        decimal openPositionQuantity,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(state.PartialTakeProfitOrderId))
        {
            return false;
        }

        var partialOrder = await TryFetchOrderAsync(
            dataProvider,
            state.PartialTakeProfitOrderId,
            cancellationToken
        );
        if (partialOrder is null)
        {
            return false;
        }

        if (partialOrder.FilledAt is not null)
        {
            state.PartialTakeProfitFilled = true;
            state.PartialTakeProfitFilledAtUtc = partialOrder.FilledAt;
            state.PartialTakeProfitOrderId = null;
            state.RemainingRunnerQuantity = ResolveOrderQuantityForLive(
                openPositionQuantity,
                _options.Value.UseWholeShareQuantity
            );
            return true;
        }

        if (IsTerminalOrderStatus(partialOrder.Status))
        {
            _logger.LogWarning(
                "Partial take-profit order {OrderId} for {Symbol} reached terminal status {Status} without fill. Clearing order id for retry.",
                partialOrder.OrderId,
                state.Opportunity.Symbol,
                partialOrder.Status
            );
            state.PartialTakeProfitOrderId = null;
            return true;
        }

        return false;
    }

    private async Task<bool> EnsureEquityTrailingStopOrderAsync(
        ITradingDataProvider dataProvider,
        OpportunityRuntimeState state,
        TradingOrderSide exitSide,
        decimal openPositionQuantity,
        CancellationToken cancellationToken
    )
    {
        if (!string.IsNullOrWhiteSpace(state.EquityTrailingStopOrderId))
        {
            return false;
        }

        var quantity = ResolveOrderQuantityForLive(openPositionQuantity, _options.Value.UseWholeShareQuantity);
        if (quantity <= 0m)
        {
            return false;
        }

        var trailingDistance = ResolveLiveTrailingDistance(state);
        if (trailingDistance <= 0m)
        {
            return false;
        }

        try
        {
            var trailingOrder = await dataProvider.SubmitEquityTrailingStopOrderAsync(
                new TradingEquityTrailingStopOrderRequest(
                    state.Opportunity.Symbol,
                    exitSide,
                    quantity,
                    trailingDistance
                ),
                cancellationToken
            );
            state.EquityTrailingStopOrderId = trailingOrder.OrderId;
            state.LiveTrailingStopPrice = trailingDistance;
            state.LiveTrailingStopActivatedAtUtc = DateTimeOffset.UtcNow;
            _logger.LogInformation(
                "Equity trailing-stop order placed for {Symbol}: OrderId={OrderId}, TrailPrice={TrailPrice}, Quantity={Quantity}.",
                state.Opportunity.Symbol,
                trailingOrder.OrderId,
                trailingDistance,
                quantity
            );
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to place equity trailing-stop order for {Symbol}.",
                state.Opportunity.Symbol
            );
            return false;
        }
    }

    private async Task<bool> TryFinalizeEquityExitOrderIfFilledAsync(
        ITradingDataProvider dataProvider,
        ITradingTradePersistenceService tradePersistence,
        OpportunityRuntimeState state,
        TradingOrderSnapshot parentOrder,
        string? exitOrderId,
        string exitReason,
        DateTimeOffset marketOpenUtc,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(exitOrderId))
        {
            return false;
        }

        var exitOrder = await TryFetchOrderAsync(dataProvider, exitOrderId, cancellationToken);
        if (exitOrder is null)
        {
            return false;
        }

        if (exitOrder.FilledAt is null)
        {
            if (!IsTerminalOrderStatus(exitOrder.Status))
            {
                return false;
            }

            if (state.EquityTrailingStopOrderId == exitOrderId)
            {
                state.EquityTrailingStopOrderId = null;
            }

            if (state.EquityStopLossOrderId == exitOrderId)
            {
                state.EquityStopLossOrderId = null;
            }

            if (state.PartialTakeProfitOrderId == exitOrderId)
            {
                state.PartialTakeProfitOrderId = null;
            }

            return true;
        }

        var exitFilledAtUtc = exitOrder.FilledAt.Value;
        state.ExitAuditLogged = true;
        state.ExitFilledAtUtc = exitFilledAtUtc;
        state.PendingExitOrderId = null;
        state.PendingExitReason = null;
        state.EquityStopLossOrderId = null;
        state.EquityTrailingStopOrderId = null;
        state.PartialTakeProfitOrderId = null;

        var feeActivities = await LoadFeeActivitiesSafeAsync(dataProvider, cancellationToken);
        var estimatedSpreadBps = Math.Max(0m, _options.Value.BacktestEstimatedSpreadBps);

        try
        {
            await tradePersistence.RecordExitFillAsync(
                state.OrderId!,
                parentOrder,
                exitOrder,
                exitReason,
                feeActivities,
                estimatedSpreadBps,
                DateTimeOffset.UtcNow,
                cancellationToken
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to persist equity trailing stop/partial exit fill for {Symbol} with order {OrderId}.",
                state.Opportunity.Symbol,
                state.OrderId
            );
        }

        var exitBarContext = await BuildLiveTradeBarContextAsync(
            dataProvider,
            state.Opportunity.Symbol,
            marketOpenUtc,
            exitFilledAtUtc,
            cancellationToken
        );
        state.ExitBarTimestampUtc = exitBarContext?.BarTimestampUtc;
        state.ExitBarIndex = exitBarContext?.BarIndex;

        return true;
    }

    private async Task<decimal> GetOpenUnderlyingPositionQuantityAsync(
        ITradingDataProvider dataProvider,
        string underlyingSymbol,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var positions = await dataProvider.GetPositionsAsync(cancellationToken);
            var position = positions.FirstOrDefault(x =>
                x.Quantity != 0m && SymbolMatchesUnderlying(underlyingSymbol, x.Symbol)
            );
            return position is null ? 0m : Math.Abs(position.Quantity);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to read open position quantity for {Symbol}.",
                underlyingSymbol
            );
            return 0m;
        }
    }

    private async Task<bool> TryPlaceOptionExitBracketAsync(
        ITradingDataProvider dataProvider,
        OpportunityRuntimeState state,
        TradingOrderSnapshot order,
        CancellationToken cancellationToken
    )
    {
        var stateChanged = TryRebaseOptionRiskLevelsFromEntryFill(
            state,
            order.FilledAveragePrice,
            _options.Value.RewardToRiskRatio
        );

        if (!string.IsNullOrWhiteSpace(state.OptionStopLossOrderId)
            && !string.IsNullOrWhiteSpace(state.OptionTakeProfitOrderId))
        {
            return stateChanged;
        }

        if (state.OptionStopLossPrice is not decimal stopPrice
            || state.OptionTakeProfitPrice is not decimal takeProfitPrice
            || stopPrice <= 0m
            || takeProfitPrice <= 0m)
        {
            _logger.LogWarning(
                "Cannot place option exit bracket for {Symbol}: missing planned option stop/take-profit prices.",
                state.Opportunity.Symbol
            );
            return stateChanged;
        }

        var quantityToProtect = order.FilledQuantity > 0m ? order.FilledQuantity : order.Quantity;
        var wholeContracts = (int)decimal.Floor(quantityToProtect);
        if (wholeContracts <= 0)
        {
            _logger.LogWarning(
                "Cannot place option exit bracket for {Symbol}: non-positive filled quantity on entry order {OrderId}.",
                state.Opportunity.Symbol,
                state.OrderId
            );
            return stateChanged;
        }

        if (string.IsNullOrWhiteSpace(state.OptionStopLossOrderId))
        {
            try
            {
                var stopOrder = await dataProvider.SubmitOptionStopLossOrderAsync(
                    new TradingOptionStopLossOrderRequest(order.Symbol, wholeContracts, stopPrice),
                    cancellationToken
                );
                state.OptionStopLossOrderId = stopOrder.OrderId;
                stateChanged = true;

                _logger.LogInformation(
                    "Option stop-loss order placed for {Symbol}: OptionSymbol={OptionSymbol} OrderId={OrderId} StopPrice={StopPrice} Contracts={Contracts}.",
                    state.Opportunity.Symbol,
                    order.Symbol,
                    stopOrder.OrderId,
                    stopPrice,
                    wholeContracts
                );
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to submit option stop-loss order for {Symbol} (OptionSymbol={OptionSymbol}). Will retry on next audit tick.",
                    state.Opportunity.Symbol,
                    order.Symbol
                );
            }
        }

        if (string.IsNullOrWhiteSpace(state.OptionTakeProfitOrderId))
        {
            try
            {
                var takeProfitOrder = await dataProvider.SubmitOptionLimitOrderAsync(
                    new TradingOptionLimitOrderRequest(
                        order.Symbol,
                        TradingOrderSide.Sell,
                        wholeContracts,
                        takeProfitPrice
                    ),
                    cancellationToken
                );
                state.OptionTakeProfitOrderId = takeProfitOrder.OrderId;
                stateChanged = true;

                _logger.LogInformation(
                    "Option take-profit order placed for {Symbol}: OptionSymbol={OptionSymbol} OrderId={OrderId} LimitPrice={LimitPrice} Contracts={Contracts}.",
                    state.Opportunity.Symbol,
                    order.Symbol,
                    takeProfitOrder.OrderId,
                    takeProfitPrice,
                    wholeContracts
                );
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to submit option take-profit order for {Symbol} (OptionSymbol={OptionSymbol}). Will retry on next audit tick.",
                    state.Opportunity.Symbol,
                    order.Symbol
                );
            }
        }

        return stateChanged;
    }

    private async Task<bool> TryFinalizeOptionExitBracketAsync(
        ITradingDataProvider dataProvider,
        ITradingTradePersistenceService tradePersistence,
        OpportunityRuntimeState state,
        TradingOrderSnapshot parentOrder,
        DateTimeOffset marketOpenUtc,
        CancellationToken cancellationToken
    )
    {
        var stateChanged =
            await TryPlaceOptionExitBracketAsync(dataProvider, state, parentOrder, cancellationToken);

        var stopOrder = await TryFetchOrderAsync(dataProvider, state.OptionStopLossOrderId, cancellationToken);
        var takeProfitOrder =
            await TryFetchOrderAsync(dataProvider, state.OptionTakeProfitOrderId, cancellationToken);

        var (filledExit, filledExitReason, siblingOrderId) =
            ResolveOptionExitFill(stopOrder, takeProfitOrder);

        if (filledExit is null)
        {
            stateChanged =
                await ReplaceTerminatedOptionExitLegsAsync(
                    dataProvider,
                    state,
                    parentOrder,
                    stopOrder,
                    takeProfitOrder,
                    cancellationToken
                )
                || stateChanged;
            return stateChanged;
        }

        if (!string.IsNullOrWhiteSpace(siblingOrderId))
        {
            try
            {
                await dataProvider.CancelOrderAsync(siblingOrderId!, cancellationToken);
                _logger.LogInformation(
                    "Cancelled sibling option exit order {OrderId} for {Symbol} after {ExitReason} fill.",
                    siblingOrderId,
                    state.Opportunity.Symbol,
                    filledExitReason
                );
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to cancel sibling option exit order {OrderId} for {Symbol}.",
                    siblingOrderId,
                    state.Opportunity.Symbol
                );
            }
        }

        var exitFilledAtUtc = filledExit.FilledAt!.Value;
        state.ExitAuditLogged = true;
        state.ExitFilledAtUtc = exitFilledAtUtc;
        state.OptionStopLossOrderId = null;
        state.OptionTakeProfitOrderId = null;
        state.PendingExitOrderId = null;
        state.PendingExitReason = null;
        stateChanged = true;

        var feeActivities = await LoadFeeActivitiesSafeAsync(dataProvider, cancellationToken);
        var estimatedSpreadBps = Math.Max(0m, _options.Value.BacktestEstimatedSpreadBps);

        try
        {
            await tradePersistence.RecordExitFillAsync(
                state.OrderId!,
                parentOrder,
                filledExit,
                filledExitReason,
                feeActivities,
                estimatedSpreadBps,
                DateTimeOffset.UtcNow,
                cancellationToken
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to persist option exit fill for {Symbol} with order {OrderId}.",
                state.Opportunity.Symbol,
                state.OrderId
            );
        }

        var exitBarContext = await BuildLiveTradeBarContextAsync(
            dataProvider,
            state.Opportunity.Symbol,
            marketOpenUtc,
            exitFilledAtUtc,
            cancellationToken
        );
        state.ExitBarTimestampUtc = exitBarContext?.BarTimestampUtc;
        state.ExitBarIndex = exitBarContext?.BarIndex;

        var entryFilledAt =
            state.EntryFilledAtUtc
            ?? parentOrder.FilledAt
            ?? state.OrderSubmittedAtUtc
            ?? exitFilledAtUtc;
        var openDuration = exitFilledAtUtc - entryFilledAt;
        if (openDuration < TimeSpan.Zero)
        {
            openDuration = TimeSpan.Zero;
        }

        int? barsOpen = null;
        if (state.EntryBarIndex is int entryBarIndex && state.ExitBarIndex is int exitBarIndex)
        {
            barsOpen = Math.Max(0, exitBarIndex - entryBarIndex);
        }

        _logger.LogInformation(
            "Live option close audit for {Symbol}: {Payload}",
            state.Opportunity.Symbol,
            JsonSerializer.Serialize(
                new
                {
                    Symbol = state.Opportunity.Symbol,
                    Direction = state.Opportunity.Direction.ToString(),
                    OrderId = state.OrderId,
                    EntryFilledAtUtc = entryFilledAt,
                    EntryBarTimestampUtc = state.EntryBarTimestampUtc,
                    EntryBarIndex = state.EntryBarIndex,
                    ExitFilledAtUtc = exitFilledAtUtc,
                    ExitBarTimestampUtc = state.ExitBarTimestampUtc,
                    ExitBarIndex = state.ExitBarIndex,
                    BarsOpen = barsOpen,
                    OpenDurationMinutes = decimal.Round((decimal)openDuration.TotalMinutes, 2),
                    ExitReason = filledExitReason,
                    ExitOrderId = filledExit.OrderId,
                    ExitOrderType = filledExit.OrderType,
                    OptionStopLoss = state.OptionStopLossPrice,
                    OptionTakeProfit = state.OptionTakeProfitPrice,
                }
            )
        );

        return stateChanged;
    }

    private async Task<TradingOrderSnapshot?> TryFetchOrderAsync(
        ITradingDataProvider dataProvider,
        string? orderId,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(orderId))
        {
            return null;
        }

        if (_streamingCache.TryGetOrder(orderId!, out var cachedOrder))
        {
            return cachedOrder;
        }

        try
        {
            return await dataProvider.GetOrderAsync(orderId!, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to fetch option exit order {OrderId}.",
                orderId
            );
            return null;
        }
    }

    private static (TradingOrderSnapshot? Filled, string Reason, string? SiblingOrderId) ResolveOptionExitFill(
        TradingOrderSnapshot? stopOrder,
        TradingOrderSnapshot? takeProfitOrder
    )
    {
        var stopFilled = stopOrder?.FilledAt is not null;
        var takeProfitFilled = takeProfitOrder?.FilledAt is not null;

        if (stopFilled && takeProfitFilled)
        {
            // If both filled (rare), pick whichever filled first as the canonical exit.
            return stopOrder!.FilledAt <= takeProfitOrder!.FilledAt
                ? (stopOrder, "StopLoss", takeProfitOrder.OrderId)
                : (takeProfitOrder, "TakeProfit", stopOrder.OrderId);
        }

        if (stopFilled)
        {
            return (stopOrder, "StopLoss", takeProfitOrder?.OrderId);
        }

        if (takeProfitFilled)
        {
            return (takeProfitOrder, "TakeProfit", stopOrder?.OrderId);
        }

        return (null, string.Empty, null);
    }

    private async Task<bool> ReplaceTerminatedOptionExitLegsAsync(
        ITradingDataProvider dataProvider,
        OpportunityRuntimeState state,
        TradingOrderSnapshot parentOrder,
        TradingOrderSnapshot? stopOrder,
        TradingOrderSnapshot? takeProfitOrder,
        CancellationToken cancellationToken
    )
    {
        var stateChanged = false;

        if (stopOrder is not null
            && stopOrder.FilledAt is null
            && IsTerminalOrderStatus(stopOrder.Status))
        {
            _logger.LogWarning(
                "Option stop-loss order {OrderId} for {Symbol} reached terminal status {Status} without fill. Resubmitting.",
                stopOrder.OrderId,
                state.Opportunity.Symbol,
                stopOrder.Status
            );
            state.OptionStopLossOrderId = null;
            stateChanged = true;
        }

        if (takeProfitOrder is not null
            && takeProfitOrder.FilledAt is null
            && IsTerminalOrderStatus(takeProfitOrder.Status))
        {
            _logger.LogWarning(
                "Option take-profit order {OrderId} for {Symbol} reached terminal status {Status} without fill. Resubmitting.",
                takeProfitOrder.OrderId,
                state.Opportunity.Symbol,
                takeProfitOrder.Status
            );
            state.OptionTakeProfitOrderId = null;
            stateChanged = true;
        }

        if (stateChanged)
        {
            stateChanged =
                await TryPlaceOptionExitBracketAsync(dataProvider, state, parentOrder, cancellationToken)
                || stateChanged;
        }

        return stateChanged;
    }

    private async Task<bool> TryFinalizePendingExitOrderAsync(
        ITradingDataProvider dataProvider,
        ITradingTradePersistenceService tradePersistence,
        OpportunityRuntimeState state,
        TradingOrderSnapshot parentOrder,
        DateTimeOffset marketOpenUtc,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(state.PendingExitOrderId))
        {
            return false;
        }

        var exitOrder = _streamingCache.TryGetOrder(state.PendingExitOrderId, out var streamExitOrder)
            ? streamExitOrder
            : await dataProvider.GetOrderAsync(state.PendingExitOrderId, cancellationToken);

        if (exitOrder is null)
        {
            return false;
        }

        if (exitOrder.FilledAt is null)
        {
            if (!IsTerminalOrderStatus(exitOrder.Status))
            {
                return false;
            }

            _logger.LogWarning(
                "Option close order {ExitOrderId} for {Symbol} reached terminal status {Status} without fill. Clearing pending exit so strategy can retry.",
                state.PendingExitOrderId,
                state.Opportunity.Symbol,
                exitOrder.Status
            );
            state.PendingExitOrderId = null;
            state.PendingExitReason = null;
            return true;
        }

        var exitFilledAtUtc = exitOrder.FilledAt.Value;
        state.ExitAuditLogged = true;
        state.ExitFilledAtUtc = exitFilledAtUtc;
        state.PendingExitOrderId = null;
        var exitReason = string.IsNullOrWhiteSpace(state.PendingExitReason)
            ? "UnderlyingTargetExit"
            : state.PendingExitReason!;
        state.PendingExitReason = null;
        var feeActivities = await LoadFeeActivitiesSafeAsync(dataProvider, cancellationToken);
        var estimatedSpreadBps = Math.Max(0m, _options.Value.BacktestEstimatedSpreadBps);

        try
        {
            await tradePersistence.RecordExitFillAsync(
                state.OrderId!,
                parentOrder,
                exitOrder,
                exitReason,
                feeActivities,
                estimatedSpreadBps,
                DateTimeOffset.UtcNow,
                cancellationToken
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to persist option close fill for {Symbol} with order {OrderId}.",
                state.Opportunity.Symbol,
                state.OrderId
            );
        }

        var exitBarContext = await BuildLiveTradeBarContextAsync(
            dataProvider,
            state.Opportunity.Symbol,
            marketOpenUtc,
            exitFilledAtUtc,
            cancellationToken
        );
        state.ExitBarTimestampUtc = exitBarContext?.BarTimestampUtc;
        state.ExitBarIndex = exitBarContext?.BarIndex;

        var entryFilledAt =
            state.EntryFilledAtUtc
            ?? parentOrder.FilledAt
            ?? state.OrderSubmittedAtUtc
            ?? exitFilledAtUtc;
        var openDuration = exitFilledAtUtc - entryFilledAt;
        if (openDuration < TimeSpan.Zero)
        {
            openDuration = TimeSpan.Zero;
        }

        int? barsOpen = null;
        if (state.EntryBarIndex is int entryBarIndex && state.ExitBarIndex is int exitBarIndex)
        {
            barsOpen = Math.Max(0, exitBarIndex - entryBarIndex);
        }

        _logger.LogInformation(
            "Live trade close audit for {Symbol}: {Payload}",
            state.Opportunity.Symbol,
            JsonSerializer.Serialize(
                new
                {
                    Symbol = state.Opportunity.Symbol,
                    Direction = state.Opportunity.Direction.ToString(),
                    OrderId = state.OrderId,
                    EntryFilledAtUtc = entryFilledAt,
                    EntryBarTimestampUtc = state.EntryBarTimestampUtc,
                    EntryBarIndex = state.EntryBarIndex,
                    ExitFilledAtUtc = exitFilledAtUtc,
                    ExitBarTimestampUtc = state.ExitBarTimestampUtc,
                    ExitBarIndex = state.ExitBarIndex,
                    BarsOpen = barsOpen,
                    OpenDurationMinutes = decimal.Round((decimal)openDuration.TotalMinutes, 2),
                    ExitReason = exitReason,
                    ExitOrderType = exitOrder.OrderType,
                    StopLoss = state.StopLossPrice,
                    TakeProfit = state.TakeProfitPrice,
                }
            )
        );

        return true;
    }

    private async Task<LiveTradeBarContext?> BuildLiveTradeBarContextAsync(
        ITradingDataProvider dataProvider,
        string symbol,
        DateTimeOffset marketOpenUtc,
        DateTimeOffset eventTimestampUtc,
        CancellationToken cancellationToken
    )
    {
        var bars = (
            await dataProvider.GetBarsAsync(
                symbol,
                marketOpenUtc,
                eventTimestampUtc.AddMinutes(1),
                1000,
                cancellationToken
            )
        ).OrderBy(x => x.Timestamp).ToArray();
        if (bars.Length == 0)
        {
            return null;
        }

        var barIndex = FindBarIndex(bars, eventTimestampUtc);
        var safeIndex = Math.Clamp(barIndex - 1, 0, bars.Length - 1);
        return new LiveTradeBarContext(barIndex, bars[safeIndex].Timestamp);
    }

    private async Task<TradingBarSnapshot[]> GetSessionBarsAsync(
        ITradingDataProvider dataProvider,
        string symbol,
        DateTimeOffset marketOpenUtc,
        CancellationToken cancellationToken
    )
    {
        var streamBars = _streamingCache.GetBars(symbol, marketOpenUtc, DateTimeOffset.UtcNow, 500);
        if (streamBars.Count >= 6)
        {
            return streamBars.OrderBy(x => x.Timestamp).ToArray();
        }

        return (
            await dataProvider.GetBarsAsync(
                symbol,
                marketOpenUtc,
                DateTimeOffset.UtcNow,
                500,
                cancellationToken
            )
        ).OrderBy(x => x.Timestamp).ToArray();
    }

    private static int FindBarIndex(IReadOnlyList<TradingBarSnapshot> bars, DateTimeOffset timestamp)
    {
        for (var index = 0; index < bars.Count; index++)
        {
            if (bars[index].Timestamp >= timestamp)
            {
                return index + 1;
            }
        }

        return bars.Count;
    }

    private static int? FindBarIndexByTimestamp(
        IReadOnlyList<TradingBarSnapshot> bars,
        DateTimeOffset timestamp
    )
    {
        for (var index = 0; index < bars.Count; index++)
        {
            if (bars[index].Timestamp == timestamp)
            {
                return index;
            }
        }

        return null;
    }

    private static bool IsAcceptedRetestValidation(
        TradingDirection expectedDirection,
        RetestVerificationResult? validation,
        int minimumRetestScore
    )
    {
        if (validation is null)
        {
            return false;
        }

        if (validation.Direction != expectedDirection)
        {
            return false;
        }

        if (!validation.IsValidRetest)
        {
            return false;
        }

        if (!validation.BreakoutConfirmed || !validation.RetestConfirmed)
        {
            return false;
        }

        return validation.Score >= minimumRetestScore;
    }

    private static RetestVerificationResult BuildStrategyOnlyRetestValidation(
        string symbol,
        TradingDirection direction,
        TradingBarSnapshot retestBar
    )
    {
        var continuationBias = direction == TradingDirection.Bullish ? "Bullish" : "Bearish";
        return new RetestVerificationResult(
            symbol,
            direction,
            true,
            100,
            0m,
            0m,
            true,
            "strategy-only",
            "AI retest validation is disabled; breakout confirmation delegated to strategy signal chain.",
            true,
            "strategy-only",
            $"Retest at {retestBar.Timestamp:O} accepted via breakout-retest strategy rules.",
            true,
            continuationBias,
            null,
            "Accepted by deterministic breakout-retest strategy because the retest pattern met rule thresholds.",
            "AI retest validation agent disabled by configuration."
        );
    }

    private static TradingOrderSide ResolveExitSideForDirection(TradingDirection direction)
    {
        return direction == TradingDirection.Bullish ? TradingOrderSide.Sell : TradingOrderSide.Buy;
    }

    private static bool IsTakeProfitTriggered(
        TradingDirection direction,
        decimal referencePrice,
        decimal takeProfitPrice
    )
    {
        if (referencePrice <= 0m || takeProfitPrice <= 0m)
        {
            return false;
        }

        return direction switch
        {
            TradingDirection.Bullish => referencePrice >= takeProfitPrice,
            TradingDirection.Bearish => referencePrice <= takeProfitPrice,
            _ => false,
        };
    }

    private static bool TryRebaseUnderlyingRiskLevelsFromEntryFill(
        OpportunityRuntimeState state,
        decimal filledAveragePrice,
        decimal rewardToRiskRatio
    )
    {
        if (!state.UseTrailingStopLoss || filledAveragePrice <= 0m)
        {
            return false;
        }

        var riskPerUnit = state.InitialRiskPerUnit ?? 0m;
        if (riskPerUnit <= 0m)
        {
            return false;
        }

        var updatedPlan = BuildRiskAnchoredTradePlan(
            state.Opportunity.Direction,
            filledAveragePrice,
            riskPerUnit,
            Math.Max(2m, rewardToRiskRatio)
        );
        if (updatedPlan is null)
        {
            return false;
        }

        var changed =
            state.PlannedEntryPrice != updatedPlan.EntryPrice
            || state.StopLossPrice != updatedPlan.StopLossPrice
            || state.TakeProfitPrice != updatedPlan.TakeProfitPrice;

        state.PlannedEntryPrice = updatedPlan.EntryPrice;
        state.StopLossPrice = updatedPlan.StopLossPrice;
        state.TakeProfitPrice = updatedPlan.TakeProfitPrice;
        return changed;
    }

    private static bool TryRebaseOptionRiskLevelsFromEntryFill(
        OpportunityRuntimeState state,
        decimal filledAveragePrice,
        decimal rewardToRiskRatio
    )
    {
        if (
            filledAveragePrice <= 0m
            || state.OptionStopLossPrice is not decimal stopLossPrice
            || state.OptionTakeProfitPrice is not decimal takeProfitPrice
            || stopLossPrice <= 0m
            || takeProfitPrice <= 0m
        )
        {
            return false;
        }

        var normalizedRr = Math.Max(2m, rewardToRiskRatio);
        var impliedRiskPerUnit = (takeProfitPrice - stopLossPrice) / (normalizedRr + 1m);
        if (impliedRiskPerUnit <= 0m)
        {
            return false;
        }

        var updatedPlan = BuildRiskAnchoredLongPlan(
            filledAveragePrice,
            impliedRiskPerUnit,
            normalizedRr
        );
        if (updatedPlan is null)
        {
            return false;
        }

        var changed =
            state.OptionStopLossPrice != updatedPlan.StopLossPrice
            || state.OptionTakeProfitPrice != updatedPlan.TakeProfitPrice;
        state.OptionStopLossPrice = updatedPlan.StopLossPrice;
        state.OptionTakeProfitPrice = updatedPlan.TakeProfitPrice;
        return changed;
    }

    private static TradePlan? BuildRiskAnchoredTradePlan(
        TradingDirection direction,
        decimal entryPrice,
        decimal riskPerUnit,
        decimal rewardToRiskRatio
    )
    {
        if (entryPrice <= 0m || riskPerUnit <= 0m || rewardToRiskRatio <= 0m)
        {
            return null;
        }

        var stopLoss = direction switch
        {
            TradingDirection.Bullish => entryPrice - riskPerUnit,
            TradingDirection.Bearish => entryPrice + riskPerUnit,
            _ => 0m,
        };
        var takeProfit = direction switch
        {
            TradingDirection.Bullish => entryPrice + riskPerUnit * rewardToRiskRatio,
            TradingDirection.Bearish => entryPrice - riskPerUnit * rewardToRiskRatio,
            _ => 0m,
        };

        if (stopLoss <= 0m || takeProfit <= 0m)
        {
            return null;
        }

        return new TradePlan(entryPrice, stopLoss, takeProfit, riskPerUnit);
    }

    private static TradePlan? BuildRiskAnchoredLongPlan(
        decimal entryPrice,
        decimal riskPerUnit,
        decimal rewardToRiskRatio
    )
    {
        if (entryPrice <= 0m || riskPerUnit <= 0m || rewardToRiskRatio <= 0m)
        {
            return null;
        }

        var stopLoss = Math.Max(entryPrice - riskPerUnit, 0.01m);
        var takeProfit = entryPrice + riskPerUnit * rewardToRiskRatio;
        if (takeProfit <= entryPrice)
        {
            return null;
        }

        var normalizedRiskPerUnit = entryPrice - stopLoss;
        return normalizedRiskPerUnit <= 0m
            ? null
            : new TradePlan(entryPrice, stopLoss, takeProfit, normalizedRiskPerUnit);
    }

    private static decimal ResolveOrderQuantityForLive(decimal quantity, bool useWholeShareQuantity)
    {
        if (quantity <= 0m)
        {
            return 0m;
        }

        return useWholeShareQuantity
            ? decimal.Floor(quantity)
            : decimal.Round(quantity, 6, MidpointRounding.ToZero);
    }

    private static LivePositionSplit ResolveLivePositionSplit(
        decimal totalQuantity,
        decimal partialTakeProfitFraction,
        bool useWholeShareQuantity
    )
    {
        if (totalQuantity <= 0m)
        {
            return new LivePositionSplit(0m, 0m, false);
        }

        var fraction = Math.Clamp(partialTakeProfitFraction, 0.05m, 0.95m);
        var partialTakeProfitQuantity = totalQuantity * fraction;
        partialTakeProfitQuantity = useWholeShareQuantity
            ? decimal.Floor(partialTakeProfitQuantity)
            : decimal.Round(partialTakeProfitQuantity, 6, MidpointRounding.ToZero);

        var runnerQuantity = totalQuantity - partialTakeProfitQuantity;
        runnerQuantity = useWholeShareQuantity
            ? decimal.Floor(runnerQuantity)
            : decimal.Round(runnerQuantity, 6, MidpointRounding.ToZero);

        if (partialTakeProfitQuantity <= 0m || runnerQuantity <= 0m)
        {
            return new LivePositionSplit(0m, 0m, false);
        }

        return new LivePositionSplit(partialTakeProfitQuantity, runnerQuantity, true);
    }

    private static decimal ResolvePartialTakeProfitQuantityForLive(
        OpportunityRuntimeState state,
        decimal openPositionQuantity,
        bool useWholeShareQuantity
    )
    {
        var planned = state.PlannedPartialTakeProfitQuantity ?? 0m;
        if (planned <= 0m)
        {
            return 0m;
        }

        var resolved = Math.Min(planned, openPositionQuantity);
        return ResolveOrderQuantityForLive(resolved, useWholeShareQuantity);
    }

    private decimal ResolveLiveTrailingDistance(OpportunityRuntimeState state)
    {
        var options = _options.Value;
        var riskPerUnit = state.InitialRiskPerUnit ?? 0m;
        var riskMultiple = Math.Max(0.1m, options.LiveTrailingStopRiskMultiple);
        var trailingDistance = Math.Max(0.01m, riskPerUnit * riskMultiple);

        if (!options.LiveTrailingStopBreakEvenProtection)
        {
            return decimal.Round(trailingDistance, 4);
        }

        var entryPrice = state.PlannedEntryPrice ?? 0m;
        if (entryPrice <= 0m || state.TakeProfitPrice is not decimal takeProfitPrice || takeProfitPrice <= 0m)
        {
            return decimal.Round(trailingDistance, 4);
        }

        var rewardFromEntry = Math.Abs(takeProfitPrice - entryPrice);
        if (rewardFromEntry <= 0m)
        {
            return decimal.Round(trailingDistance, 4);
        }

        // Ensure the initial trailing stop after TP sits at or better than break-even.
        trailingDistance = Math.Min(trailingDistance, rewardFromEntry);
        return decimal.Round(Math.Max(0.01m, trailingDistance), 4);
    }

    private static StrategyThresholds BuildStrategyThresholds(TradingAutomationOptions options)
    {
        return new StrategyThresholds(
            DirectionalCloseLocation: Math.Clamp(options.BreakoutDirectionalCloseLocationThreshold, 0.5m, 0.95m),
            RetestNearRangeFraction: Math.Max(0m, options.RetestNearRangeFraction),
            RetestPierceRangeFraction: Math.Max(0m, options.RetestPierceRangeFraction),
            RetestBodyToleranceFraction: Math.Clamp(options.RetestBodyToleranceFraction, 0m, 0.5m),
            MaxMinutesBreakoutToRetest: Math.Max(0, options.MaxMinutesBreakoutToRetest)
        );
    }

    private static TradePlan EnsureMinimumUnderlyingTradePlan(
        TradingDirection direction,
        TradePlan tradePlan,
        decimal entryPrice,
        decimal stopLossBufferFraction,
        decimal rewardToRiskRatio
    )
    {
        if (entryPrice <= 0m || stopLossBufferFraction <= 0m || rewardToRiskRatio <= 0m)
        {
            return tradePlan;
        }

        var minimumRiskPerUnit = Math.Max(entryPrice * stopLossBufferFraction, 0.01m);
        if (tradePlan.RiskPerUnit >= minimumRiskPerUnit)
        {
            return tradePlan;
        }

        var stopLoss = direction switch
        {
            TradingDirection.Bullish => entryPrice - minimumRiskPerUnit,
            TradingDirection.Bearish => entryPrice + minimumRiskPerUnit,
            _ => tradePlan.StopLossPrice,
        };

        var takeProfit = direction switch
        {
            TradingDirection.Bullish => entryPrice + minimumRiskPerUnit * rewardToRiskRatio,
            TradingDirection.Bearish => entryPrice - minimumRiskPerUnit * rewardToRiskRatio,
            _ => tradePlan.TakeProfitPrice,
        };

        if (stopLoss <= 0m || takeProfit <= 0m)
        {
            return tradePlan;
        }

        return new TradePlan(entryPrice, stopLoss, takeProfit, minimumRiskPerUnit);
    }

    private static TradePlan? BuildOptionTradePlan(
        decimal optionEntryPrice,
        TradePlan underlyingTradePlan,
        decimal underlyingEntryPrice,
        decimal rewardToRiskRatio
    )
    {
        if (
            optionEntryPrice <= 0m
            || underlyingEntryPrice <= 0m
            || underlyingTradePlan.RiskPerUnit <= 0m
            || rewardToRiskRatio <= 0m
        )
        {
            return null;
        }

        var underlyingRiskFraction = underlyingTradePlan.RiskPerUnit / underlyingEntryPrice;
        if (underlyingRiskFraction <= 0m)
        {
            return null;
        }

        var optionRiskPerUnit = Math.Max(optionEntryPrice * underlyingRiskFraction, 0.01m);
        var stopLoss = Math.Max(optionEntryPrice - optionRiskPerUnit, 0.01m);
        optionRiskPerUnit = optionEntryPrice - stopLoss;
        var takeProfit = optionEntryPrice + optionRiskPerUnit * rewardToRiskRatio;

        if (takeProfit <= optionEntryPrice)
        {
            return null;
        }

        return new TradePlan(optionEntryPrice, stopLoss, takeProfit, optionRiskPerUnit);
    }

    private static IReadOnlyCollection<TradingLiveRetestAttemptSnapshot> BuildPersistedRetestAttempts(
        OpportunityRuntimeState state
    )
    {
        return state.RetestAttempts
            .OrderBy(x => x.RetestBar.Timestamp)
            .Select(attempt =>
                new TradingLiveRetestAttemptSnapshot(
                    attempt.AttemptId,
                    attempt.RetestBar.Timestamp,
                    null,
                    attempt.IsValid,
                    attempt.Score,
                    attempt.RejectionReason,
                    attempt.Validation
                )
            )
            .ToArray();
    }

    private static string DetermineExitReasonFromOrderType(string? orderType)
    {
        if (string.IsNullOrWhiteSpace(orderType))
        {
            return "ExitLegFilled";
        }

        if (orderType.Contains("stop", StringComparison.OrdinalIgnoreCase))
        {
            return "StopLoss";
        }

        if (orderType.Contains("limit", StringComparison.OrdinalIgnoreCase))
        {
            return "TakeProfit";
        }

        return "ExitLegFilled";
    }

    private static bool IsTerminalOrderStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return false;
        }

        return status.Equals("canceled", StringComparison.OrdinalIgnoreCase)
            || status.Equals("expired", StringComparison.OrdinalIgnoreCase)
            || status.Equals("rejected", StringComparison.OrdinalIgnoreCase)
            || status.Equals("done_for_day", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeOptionContractSymbol(string symbol)
    {
        return TryExtractUnderlyingFromOptionContractSymbol(symbol, out _);
    }

    private static bool TryExtractUnderlyingFromOptionContractSymbol(
        string optionSymbol,
        out string underlyingSymbol
    )
    {
        underlyingSymbol = string.Empty;
        if (string.IsNullOrWhiteSpace(optionSymbol))
        {
            return false;
        }

        var match = OptionContractSymbolRegex.Match(optionSymbol.Trim().ToUpperInvariant());
        if (!match.Success)
        {
            return false;
        }

        underlyingSymbol = match.Groups[1].Value;
        return !string.IsNullOrWhiteSpace(underlyingSymbol);
    }

    private static decimal ResolveOptionPremium(
        TradingOptionQuoteSnapshot? optionQuote,
        decimal? fallbackClosePrice
    )
    {
        if (optionQuote is not null)
        {
            if (optionQuote.AskPrice > 0m)
            {
                return optionQuote.AskPrice;
            }

            if (optionQuote.LastPrice > 0m)
            {
                return optionQuote.LastPrice;
            }
        }

        return fallbackClosePrice is decimal closePrice && closePrice > 0m ? closePrice : 0m;
    }

    private static bool ShouldSuppressOrderSubmissionRetry(AlpacaApiException exception)
    {
        return exception.StatusCode is HttpStatusCode.BadRequest
            or HttpStatusCode.Unauthorized
            or HttpStatusCode.Forbidden
            or HttpStatusCode.UnprocessableEntity;
    }

    private static string BuildSubmissionErrorSummary(AlpacaApiException exception)
    {
        var alpacaMessage = exception.AlpacaMessage;
        if (!string.IsNullOrWhiteSpace(alpacaMessage))
        {
            return string.IsNullOrWhiteSpace(exception.AlpacaCode)
                ? alpacaMessage
                : $"{exception.AlpacaCode}: {alpacaMessage}";
        }

        return exception.Message;
    }

    private static bool HasExistingExposure(
        string underlyingSymbol,
        IReadOnlyCollection<TradingPositionSnapshot> positions,
        IReadOnlyCollection<TradingOrderSnapshot> openOrders,
        out string? linkedOrderId
    )
    {
        linkedOrderId = null;

        if (
            positions.Any(x =>
                x.Quantity != 0m && SymbolMatchesUnderlying(underlyingSymbol, x.Symbol)
            )
        )
        {
            return true;
        }

        var openOrder = openOrders.FirstOrDefault(x =>
            SymbolMatchesUnderlying(underlyingSymbol, x.Symbol)
        );
        if (openOrder is null)
        {
            return false;
        }

        linkedOrderId = openOrder.OrderId;
        return true;
    }

    private static bool SymbolMatchesUnderlying(string underlyingSymbol, string candidateSymbol)
    {
        if (candidateSymbol.Equals(underlyingSymbol, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return TryExtractUnderlyingFromOptionContractSymbol(candidateSymbol, out var optionUnderlying)
            && optionUnderlying.Equals(underlyingSymbol, StringComparison.OrdinalIgnoreCase);
    }

    private static decimal ResolveConfiguredOrderQuantity(
        decimal configuredOrderQuantity,
        bool useWholeShareQuantity
    )
    {
        if (configuredOrderQuantity <= 0m)
        {
            return 0m;
        }

        return useWholeShareQuantity
            ? decimal.Floor(configuredOrderQuantity)
            : decimal.Round(configuredOrderQuantity, 6, MidpointRounding.ToZero);
    }

    private static decimal ResolveConfiguredOptionContracts(
        decimal configuredContractQuantity
    )
    {
        if (configuredContractQuantity <= 0m)
        {
            return 0m;
        }

        var wholeContracts = decimal.Floor(configuredContractQuantity);
        if (wholeContracts <= 0m)
        {
            return 0m;
        }

        return wholeContracts;
    }

    private static decimal ResolveUnderlyingReferencePrice(TradingQuoteSnapshot quote)
    {
        if (quote.LastPrice > 0m)
        {
            return quote.LastPrice;
        }

        if (quote.BidPrice > 0m && quote.AskPrice > 0m)
        {
            return (quote.BidPrice + quote.AskPrice) / 2m;
        }

        if (quote.AskPrice > 0m)
        {
            return quote.AskPrice;
        }

        return quote.BidPrice > 0m ? quote.BidPrice : 0m;
    }

    private async Task SyncClosedTradeFeesAsync(
        ITradingDataProvider dataProvider,
        ITradingTradePersistenceService tradePersistence,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken
    )
    {
        if (
            _lastFeeSyncAtUtc is DateTimeOffset lastFeeSyncAtUtc
            && utcNow - lastFeeSyncAtUtc < FeeSyncInterval
        )
        {
            return;
        }

        var feeActivities = await LoadFeeActivitiesSafeAsync(dataProvider, cancellationToken);
        _latestFeeActivities = feeActivities;
        _latestFeeCurrency = feeActivities
            .Select(x => x.Currency?.Trim())
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

        if (feeActivities.Count == 0)
        {
            _lastFeeSyncAtUtc = utcNow;
            return;
        }

        var estimatedSpreadBps = Math.Max(0m, _options.Value.BacktestEstimatedSpreadBps);
        await tradePersistence.SyncRecentClosedTradeFeesAsync(
            feeActivities,
            estimatedSpreadBps,
            utcNow,
            lookbackDays: 10,
            cancellationToken
        );
        _lastFeeSyncAtUtc = utcNow;
    }

    private async Task<IReadOnlyCollection<TradingFeeActivitySnapshot>> LoadFeeActivitiesSafeAsync(
        ITradingDataProvider dataProvider,
        CancellationToken cancellationToken
    )
    {
        try
        {
            return await dataProvider.GetFeeActivitiesAsync(500, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load Alpaca fee activities for trade fee calculation.");
            return [];
        }
    }

    private async Task<DateTimeOffset> ResolveMarketOpenUtcAsync(
        ITradingDataProvider dataProvider,
        DateOnly tradingDate,
        CancellationToken cancellationToken
    )
    {
        if (
            _lastResolvedMarketOpenDate == tradingDate
            && _lastResolvedMarketOpenUtc is DateTimeOffset cachedMarketOpenUtc
        )
        {
            return cachedMarketOpenUtc;
        }

        DateTimeOffset? marketOpenUtc = null;
        var watchlistId = _options.Value.WatchlistId?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(watchlistId))
        {
            try
            {
                marketOpenUtc = await dataProvider.GetWatchlistMarketOpenUtcAsync(
                    watchlistId,
                    tradingDate,
                    cancellationToken
                );
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to resolve market open from Alpaca watchlist {WatchlistId} for {TradingDate}.",
                    watchlistId,
                    tradingDate
                );
            }
        }

        if (marketOpenUtc is null)
        {
            marketOpenUtc = ToMarketDateTimeUtc(
                tradingDate,
                FallbackMarketOpenHour,
                FallbackMarketOpenMinute
            );
            _logger.LogInformation(
                "Using fallback market open 09:30 ET for {TradingDate}: {MarketOpenUtc}.",
                tradingDate,
                marketOpenUtc.Value
            );
        }
        else
        {
            _logger.LogInformation(
                "Resolved market open from Alpaca for {TradingDate}: {MarketOpenUtc}.",
                tradingDate,
                marketOpenUtc.Value
            );
        }

        _lastResolvedMarketOpenDate = tradingDate;
        _lastResolvedMarketOpenUtc = marketOpenUtc.Value;

        return marketOpenUtc.Value;
    }

    private static TimeZoneInfo ResolveTradingTimeZone(string configuredTimeZoneId)
    {
        if (!string.IsNullOrWhiteSpace(configuredTimeZoneId))
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(configuredTimeZoneId);
            }
            catch (TimeZoneNotFoundException)
            {
                // fall through to defaults
            }
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        }
    }

    private DateTimeOffset ToMarketDateTimeUtc(DateOnly date, int hour, int minute)
    {
        var local = new DateTime(date.Year, date.Month, date.Day, hour, minute, 0, DateTimeKind.Unspecified);
        var offset = _tradingTimeZone.GetUtcOffset(local);
        var localOffset = new DateTimeOffset(local, offset);
        return localOffset.ToUniversalTime();
    }

    private sealed class OpportunityRuntimeState
    {
        public OpportunityRuntimeState(TradingOpportunity opportunity)
        {
            Opportunity = opportunity;
        }

        public TradingOpportunity Opportunity { get; }

        public OpeningRangeSnapshot? OpeningRange { get; set; }

        public TradingBarSnapshot? BreakoutBar { get; set; }

        public DateTimeOffset? LastEvaluatedRetestTimestamp { get; set; }

        public List<RetestAttemptRuntimeState> RetestAttempts { get; } = [];

        public bool OrderPlaced { get; set; }

        public long? SentimentAnalysisId { get; set; }

        public string? OrderId { get; set; }

        public DateTimeOffset? OrderSubmittedAtUtc { get; set; }

        public DateTimeOffset? EntrySignalBarTimestampUtc { get; set; }

        public decimal? PlannedEntryPrice { get; set; }

        public decimal? StopLossPrice { get; set; }

        public decimal? TakeProfitPrice { get; set; }

        public bool EntryAuditLogged { get; set; }

        public bool ExitAuditLogged { get; set; }

        public DateTimeOffset? EntryFilledAtUtc { get; set; }

        public DateTimeOffset? ExitFilledAtUtc { get; set; }

        public DateTimeOffset? EntryBarTimestampUtc { get; set; }

        public DateTimeOffset? ExitBarTimestampUtc { get; set; }

        public int? EntryBarIndex { get; set; }

        public int? ExitBarIndex { get; set; }

        public bool OrderSubmissionRejected { get; set; }

        public string? LastOrderSubmissionError { get; set; }

        public DateTimeOffset? LastOrderSubmissionFailedAtUtc { get; set; }

        public string? TradedInstrumentSymbol { get; set; }

        public string? OptionContractType { get; set; }

        public decimal? OptionStrikePrice { get; set; }

        public DateOnly? OptionExpirationDate { get; set; }

        public string? PendingExitOrderId { get; set; }

        public string? PendingExitReason { get; set; }

        public decimal? OptionStopLossPrice { get; set; }

        public decimal? OptionTakeProfitPrice { get; set; }

        public string? OptionStopLossOrderId { get; set; }

        public string? OptionTakeProfitOrderId { get; set; }

        public bool UseTrailingStopLoss { get; set; }

        public decimal? InitialRiskPerUnit { get; set; }

        public decimal? PlannedPartialTakeProfitQuantity { get; set; }

        public decimal? PlannedRunnerQuantity { get; set; }

        public decimal? RemainingRunnerQuantity { get; set; }

        public string? EquityStopLossOrderId { get; set; }

        public string? EquityTrailingStopOrderId { get; set; }

        public string? PartialTakeProfitOrderId { get; set; }

        public bool PartialTakeProfitFilled { get; set; }

        public DateTimeOffset? PartialTakeProfitFilledAtUtc { get; set; }

        public decimal? LiveTrailingStopPrice { get; set; }

        public DateTimeOffset? LiveTrailingStopActivatedAtUtc { get; set; }

        public TradingBarSnapshot[]? LastSessionBars { get; set; }
    }

    private sealed record RetestAttemptRuntimeState(
        string AttemptId,
        TradingBarSnapshot RetestBar,
        bool IsValid,
        int Score,
        string? RejectionReason,
        RetestVerificationResult? Validation
    );

    private sealed record LivePositionSplit(
        decimal PartialTakeProfitQuantity,
        decimal RunnerQuantity,
        bool IsEnabled
    );

    private sealed class SymbolFeeAccumulator
    {
        public SymbolFeeAccumulator(string symbol)
        {
            Symbol = symbol;
        }

        public string Symbol { get; }

        public int ActivityCount { get; set; }

        public decimal TotalFees { get; set; }

        public decimal OptionFees { get; set; }

        public decimal EquityFees { get; set; }
    }

    private sealed record LiveTradeBarContext(int BarIndex, DateTimeOffset BarTimestampUtc);
}
