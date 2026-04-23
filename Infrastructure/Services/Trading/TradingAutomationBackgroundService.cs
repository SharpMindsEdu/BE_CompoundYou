using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Application.Features.Trading.Automation;
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

    private readonly IOptions<AlpacaTradingOptions> _alpacaOptions;
    private readonly ILogger<TradingAutomationBackgroundService> _logger;
    private readonly IOptions<TradingAutomationOptions> _options;
    private readonly IOptions<OpenAiTradingOptions> _openAiOptions;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ITradingAutomationStateStore _stateStore;
    private readonly IAlpacaStreamingCache _streamingCache;
    private readonly Dictionary<string, OpportunityRuntimeState> _watchStates = new(
        StringComparer.OrdinalIgnoreCase
    );

    private readonly TimeZoneInfo _tradingTimeZone;
    private DateOnly? _lastSentimentScanDate;
    private DateOnly? _lastResolvedMarketOpenDate;
    private DateTimeOffset? _lastResolvedMarketOpenUtc;
    private DateOnly? _lastStateResetDate;
    private bool _loggedDisabledMessage;
    private bool _loggedMissingApiCredentials;
    private bool _loggedMissingConfiguration;

    public TradingAutomationBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<AlpacaTradingOptions> alpacaOptions,
        IOptions<OpenAiTradingOptions> openAiOptions,
        IOptions<TradingAutomationOptions> options,
        ITradingAutomationStateStore stateStore,
        IAlpacaStreamingCache streamingCache,
        ILogger<TradingAutomationBackgroundService> logger
    )
    {
        _scopeFactory = scopeFactory;
        _alpacaOptions = alpacaOptions;
        _openAiOptions = openAiOptions;
        _options = options;
        _stateStore = stateStore;
        _streamingCache = streamingCache;
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
        using var scope = _scopeFactory.CreateScope();
        var dataProvider = scope.ServiceProvider.GetRequiredService<ITradingDataProvider>();
        var tradingSignalAgent = scope.ServiceProvider.GetRequiredService<ITradingSignalAgent>();
        var strategy = scope.ServiceProvider.GetRequiredService<RangeBreakoutRetestStrategy>();
        var tradePersistence = scope.ServiceProvider.GetRequiredService<ITradingTradePersistenceService>();

        var options = _options.Value;
        if (!options.Enabled)
        {
            if (!_loggedDisabledMessage)
            {
                _logger.LogInformation("Trading automation worker is disabled by configuration.");
                _loggedDisabledMessage = true;
            }

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

            return;
        }

        var utcNow = DateTimeOffset.UtcNow;
        var tradingNow = TimeZoneInfo.ConvertTime(utcNow, _tradingTimeZone);
        var tradingDate = DateOnly.FromDateTime(tradingNow.Date);
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
            await RestoreStateAsync(tradingDate, cancellationToken);
        }

        if (
            tradingNow.TimeOfDay >= sentimentScanTime
            && (_lastSentimentScanDate is null || _lastSentimentScanDate.Value != tradingDate)
        )
        {
            await RefreshDailyOpportunitiesAsync(
                dataProvider,
                tradingSignalAgent,
                tradingDate,
                cancellationToken
            );
        }

        if (_watchStates.Count == 0)
        {
            _streamingCache.SetSymbols([]);
            return;
        }

        _streamingCache.SetSymbols(_watchStates.Keys.ToArray());

        var marketOpenUtc = await ResolveMarketOpenUtcAsync(
            dataProvider,
            tradingDate,
            cancellationToken
        );
        if (utcNow < marketOpenUtc)
        {
            return;
        }

        var marketClock = await dataProvider.GetMarketClockAsync(cancellationToken);
        if (!marketClock.IsOpen)
        {
            return;
        }

        var openPositions = await dataProvider.GetPositionsAsync(cancellationToken);
        var openOrders = await dataProvider.GetOpenOrdersAsync(cancellationToken);

        var stateChanged = await AuditActiveOrdersAsync(
            dataProvider,
            tradePersistence,
            marketOpenUtc,
            cancellationToken
        );

        foreach (var state in _watchStates.Values.Where(x => !x.OrderPlaced).ToArray())
        {
            try
            {
                stateChanged =
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
            await PersistStateAsync(cancellationToken);
        }
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
            };
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
                x.LastEvaluatedRetestTimestamp,
                x.OrderPlaced,
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
                x.PendingExitReason
            ))
            .ToArray();

        await _stateStore.SaveAsync(
            new TradingAutomationStateSnapshot(_lastStateResetDate.Value, _lastSentimentScanDate, symbols),
            cancellationToken
        );
    }

    private async Task RefreshDailyOpportunitiesAsync(
        ITradingDataProvider dataProvider,
        ITradingSignalAgent tradingSignalAgent,
        DateOnly tradingDate,
        CancellationToken cancellationToken
    )
    {
        var options = _options.Value;
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
            return;
        }

        IReadOnlyCollection<string> eligibleSymbols = symbols;
        if (options.UseOptionsTrading)
        {
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
                return;
            }
        }

        var opportunities = await tradingSignalAgent.AnalyzeWatchlistSentimentAsync(
            eligibleSymbols,
            options.MaxOpportunities,
            tradingDate,
            cancellationToken
        );

        var selected = opportunities
            .Where(x => x.Score >= options.MinimumSentimentScore)
            .OrderByDescending(x => x.Score)
            .Take(Math.Clamp(options.MaxOpportunities, 1, 3))
            .ToArray();

        _watchStates.Clear();
        foreach (var opportunity in selected)
        {
            _watchStates[opportunity.Symbol] = new OpportunityRuntimeState(opportunity);
        }

        _lastSentimentScanDate = tradingDate;
        await PersistStateAsync(cancellationToken);
        _logger.LogInformation(
            "Daily watchlist sentiment scan completed: {Payload}",
            JsonSerializer.Serialize(
                new
                {
                    Opportunities = selected.Select(x => new
                    {
                        x.Symbol,
                        Direction = x.Direction.ToString(),
                        x.Score,
                    }),
                }
            )
        );
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
                                state.OrderSubmittedAtUtc.Value
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
            return true;
        }

        if (state.OrderSubmissionRejected)
        {
            _logger.LogDebug(
                "Skipping order submission retry for {Symbol} due to earlier non-retriable rejection: {Reason}.",
                state.Opportunity.Symbol,
                state.LastOrderSubmissionError
            );
            return false;
        }

        var bars = await GetSessionBarsAsync(
            dataProvider,
            state.Opportunity.Symbol,
            marketOpenUtc,
            cancellationToken
        );

        if (bars.Length < 6)
        {
            return false;
        }

        var openingRange = state.OpeningRange;
        if (openingRange is null)
        {
            if (!strategy.TryBuildOpeningRange(bars, marketOpenUtc, out var builtOpeningRange))
            {
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
            return false;
        }

        if (state.BreakoutBar is null)
        {
            var breakoutBar = strategy.FindBreakoutBar(
                state.Opportunity.Direction,
                openingRange,
                bars
            );
            if (breakoutBar is null)
            {
                return false;
            }

            state.BreakoutBar = breakoutBar;
            _logger.LogInformation(
                "Breakout detected for {Symbol} at {Timestamp} in {Direction} direction.",
                state.Opportunity.Symbol,
                breakoutBar.Timestamp,
                state.Opportunity.Direction
            );
        }

        var retestBar = strategy.FindRetestBar(
            state.Opportunity.Direction,
            openingRange,
            state.BreakoutBar.Timestamp,
            state.LastEvaluatedRetestTimestamp,
            bars
        );

        if (retestBar is null)
        {
            return false;
        }

        state.LastEvaluatedRetestTimestamp = retestBar.Timestamp;
        var retestValidation = await tradingSignalAgent.VerifyRetestAsync(
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
        if (retestValidation is null || retestValidation.Score < options.MinimumRetestScore)
        {
            _logger.LogInformation(
                "Retest validation rejected for {Symbol}: {Payload}",
                state.Opportunity.Symbol,
                JsonSerializer.Serialize(
                    new
                    {
                        Symbol = state.Opportunity.Symbol,
                        Direction = state.Opportunity.Direction.ToString(),
                        Score = retestValidation?.Score ?? 0,
                    }
                )
            );
            return true;
        }

        var quote = _streamingCache.TryGetQuote(state.Opportunity.Symbol, out var streamQuote)
            ? streamQuote
            : await dataProvider.GetQuoteAsync(state.Opportunity.Symbol, cancellationToken);
        var entryPrice = quote.LastPrice > 0m ? quote.LastPrice : retestBar.Close;
        var tradePlan = strategy.BuildTradePlan(
            state.Opportunity.Direction,
            entryPrice,
            retestBar,
            options.StopLossBufferPercent,
            options.RewardToRiskRatio
        );

        if (tradePlan is null)
        {
            _logger.LogWarning(
                "Trade plan could not be created for {Symbol}. Entry={EntryPrice}.",
                state.Opportunity.Symbol,
                entryPrice
            );
            return true;
        }

        var referenceEntryPrice = tradePlan.EntryPrice > 0m ? tradePlan.EntryPrice : entryPrice;
        TradingOptionContractSnapshot? selectedOptionContract = null;
        decimal orderQuantity;
        decimal plannedEntryPrice = referenceEntryPrice;
        var plannedRiskPerUnit = options.UseOptionsTrading ? 0m : tradePlan.RiskPerUnit;
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
                    return true;
                }

                plannedEntryPrice = optionPremium > 0m ? optionPremium : plannedEntryPrice;

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
                    return true;
                }

                order = await dataProvider.SubmitBracketOrderAsync(
                    new TradingBracketOrderRequest(
                        state.Opportunity.Symbol,
                        state.Opportunity.Direction,
                        orderQuantity,
                        tradePlan.StopLossPrice,
                        tradePlan.TakeProfitPrice
                    ),
                    cancellationToken
                );
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
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

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Order submission failed for {Symbol}; will retry in a future tick.",
                state.Opportunity.Symbol
            );
            return false;
        }

        state.OrderPlaced = true;
        state.OrderId = order.OrderId;
        state.OrderSubmittedAtUtc = DateTimeOffset.UtcNow;
        state.EntrySignalBarTimestampUtc = retestBar.Timestamp;
        state.PlannedEntryPrice = plannedEntryPrice;
        state.StopLossPrice = tradePlan.StopLossPrice;
        state.TakeProfitPrice = tradePlan.TakeProfitPrice;
        state.OrderSubmissionRejected = false;
        state.LastOrderSubmissionError = null;
        state.LastOrderSubmissionFailedAtUtc = null;
        state.TradedInstrumentSymbol = order.Symbol;
        state.OptionContractType = selectedOptionContract?.ContractType.ToString();
        state.OptionStrikePrice = selectedOptionContract?.StrikePrice;
        state.OptionExpirationDate = selectedOptionContract?.ExpirationDate;
        state.PendingExitOrderId = null;
        state.PendingExitReason = null;

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
                    plannedEntryPrice,
                    tradePlan.StopLossPrice,
                    tradePlan.TakeProfitPrice,
                    plannedRiskPerUnit,
                    state.Opportunity.Score,
                    retestValidation.Score,
                    retestBar.Timestamp,
                    state.OrderSubmittedAtUtc.Value
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
                    retestValidation.Score,
                    EntryPrice = plannedEntryPrice,
                    StopLoss = tradePlan.StopLossPrice,
                    TakeProfit = tradePlan.TakeProfitPrice,
                    Quantity = orderQuantity,
                    InstrumentSymbol = order.Symbol,
                    SignalRetestBarTimestampUtc = retestBar.Timestamp,
                    OrderId = order.OrderId,
                    OrderSubmittedAtUtc = state.OrderSubmittedAtUtc,
                }
            )
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
        if (string.IsNullOrWhiteSpace(state.OrderId))
        {
            return false;
        }

        var order = _streamingCache.TryGetOrder(state.OrderId, out var streamOrder)
            ? streamOrder
            : await dataProvider.GetOrderAsync(state.OrderId, cancellationToken);
        if (order is null)
        {
            return false;
        }

        var stateChanged = false;
        if (!state.EntryAuditLogged && order.FilledAt is DateTimeOffset entryFilledAtUtc)
        {
            state.EntryAuditLogged = true;
            state.EntryFilledAtUtc = entryFilledAtUtc;
            stateChanged = true;

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
        }

        if (state.ExitAuditLogged)
        {
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
                return stateChanged;
            }
        }

        var exitLeg = order.Legs
            .Where(x => x.FilledAt is not null)
            .OrderByDescending(x => x.FilledAt)
            .FirstOrDefault();
        if (exitLeg is null)
        {
            if (LooksLikeOptionContractSymbol(order.Symbol))
            {
                stateChanged =
                    await TrySubmitOptionExitOrderAsync(
                        dataProvider,
                        state,
                        order,
                        cancellationToken
                    )
                    || stateChanged;
            }

            return stateChanged;
        }

        var exitFilledAtUtc = exitLeg.FilledAt!.Value;
        state.ExitAuditLogged = true;
        state.ExitFilledAtUtc = exitFilledAtUtc;
        state.PendingExitOrderId = null;
        state.PendingExitReason = null;
        stateChanged = true;

        var exitReason = DetermineExitReasonFromOrderType(exitLeg.OrderType);
        try
        {
            await tradePersistence.RecordExitFillAsync(
                state.OrderId!,
                order,
                exitLeg,
                exitReason,
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

        return stateChanged;
    }

    private async Task<bool> TrySubmitOptionExitOrderAsync(
        ITradingDataProvider dataProvider,
        OpportunityRuntimeState state,
        TradingOrderSnapshot order,
        CancellationToken cancellationToken
    )
    {
        if (!state.EntryAuditLogged || !string.IsNullOrWhiteSpace(state.PendingExitOrderId))
        {
            return false;
        }

        var underlyingLastPrice = _streamingCache.TryGetQuote(state.Opportunity.Symbol, out var streamQuote)
            ? streamQuote.LastPrice
            : (await dataProvider.GetQuoteAsync(state.Opportunity.Symbol, cancellationToken)).LastPrice;

        if (
            !TryResolveUnderlyingExitTrigger(
                state.Opportunity.Direction,
                state.StopLossPrice,
                state.TakeProfitPrice,
                underlyingLastPrice,
                out var exitReason
            )
        )
        {
            return false;
        }

        var quantityToClose = order.FilledQuantity > 0m ? order.FilledQuantity : order.Quantity;
        var wholeContracts = (int)decimal.Floor(quantityToClose);
        if (wholeContracts <= 0)
        {
            _logger.LogWarning(
                "Unable to place option close order for {Symbol}: non-positive filled quantity on entry order {OrderId}.",
                state.Opportunity.Symbol,
                state.OrderId
            );
            return false;
        }

        var closingSide = order.Side.Equals("sell", StringComparison.OrdinalIgnoreCase)
            ? TradingOrderSide.Buy
            : TradingOrderSide.Sell;
        var closeOrder = await dataProvider.SubmitOptionOrderAsync(
            new TradingOptionOrderRequest(order.Symbol, closingSide, wholeContracts),
            cancellationToken
        );

        state.PendingExitOrderId = closeOrder.OrderId;
        state.PendingExitReason = exitReason;

        _logger.LogInformation(
            "Option close order submitted for {Symbol}: {Payload}",
            state.Opportunity.Symbol,
            JsonSerializer.Serialize(
                new
                {
                    Symbol = state.Opportunity.Symbol,
                    OptionSymbol = order.Symbol,
                    ParentOrderId = state.OrderId,
                    ExitOrderId = closeOrder.OrderId,
                    ExitReason = exitReason,
                    UnderlyingPrice = underlyingLastPrice,
                    Contracts = wholeContracts,
                }
            )
        );

        return true;
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

        try
        {
            await tradePersistence.RecordExitFillAsync(
                state.OrderId!,
                parentOrder,
                exitOrder,
                exitReason,
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

    private static bool TryResolveUnderlyingExitTrigger(
        TradingDirection direction,
        decimal? stopLossPrice,
        decimal? takeProfitPrice,
        decimal underlyingPrice,
        out string exitReason
    )
    {
        exitReason = string.Empty;
        if (
            underlyingPrice <= 0m
            || stopLossPrice is not decimal stopLoss
            || takeProfitPrice is not decimal takeProfit
        )
        {
            return false;
        }

        if (direction == TradingDirection.Bullish)
        {
            if (underlyingPrice <= stopLoss)
            {
                exitReason = "StopLoss";
                return true;
            }

            if (underlyingPrice >= takeProfit)
            {
                exitReason = "TakeProfit";
                return true;
            }

            return false;
        }

        if (underlyingPrice >= stopLoss)
        {
            exitReason = "StopLoss";
            return true;
        }

        if (underlyingPrice <= takeProfit)
        {
            exitReason = "TakeProfit";
            return true;
        }

        return false;
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

        public bool OrderPlaced { get; set; }

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
    }

    private sealed record LiveTradeBarContext(int BarIndex, DateTimeOffset BarTimestampUtc);
}
