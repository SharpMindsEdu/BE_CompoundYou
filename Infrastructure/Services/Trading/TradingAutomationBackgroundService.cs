using System.Text.Json;
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
    private static readonly TimeZoneInfo MarketTimeZone = ResolveTradingTimeZone("Eastern Standard Time");

    private readonly IOptions<AlpacaTradingOptions> _alpacaOptions;
    private readonly ILogger<TradingAutomationBackgroundService> _logger;
    private readonly IOptions<TradingAutomationOptions> _options;
    private readonly IOptions<OpenAiTradingOptions> _openAiOptions;
    private readonly IServiceScopeFactory _scopeFactory;
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
        ILogger<TradingAutomationBackgroundService> logger
    )
    {
        _scopeFactory = scopeFactory;
        _alpacaOptions = alpacaOptions;
        _openAiOptions = openAiOptions;
        _options = options;
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

        if (string.IsNullOrWhiteSpace(options.WatchlistId) || options.OrderQuantity <= 0m)
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
            _watchStates.Clear();
            _lastResolvedMarketOpenDate = null;
            _lastResolvedMarketOpenUtc = null;
            _lastStateResetDate = tradingDate;
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
            return;
        }

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

        await AuditActiveOrdersAsync(dataProvider, marketOpenUtc, cancellationToken);

        foreach (var state in _watchStates.Values.Where(x => !x.OrderPlaced).ToArray())
        {
            await EvaluateOpportunityAsync(
                strategy,
                dataProvider,
                tradingSignalAgent,
                state,
                tradingDate,
                marketOpenUtc,
                cancellationToken
            );
        }
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
            return;
        }

        var opportunities = await tradingSignalAgent.AnalyzeWatchlistSentimentAsync(
            symbols,
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

    private async Task EvaluateOpportunityAsync(
        RangeBreakoutRetestStrategy strategy,
        ITradingDataProvider dataProvider,
        ITradingSignalAgent tradingSignalAgent,
        OpportunityRuntimeState state,
        DateOnly tradingDate,
        DateTimeOffset marketOpenUtc,
        CancellationToken cancellationToken
    )
    {
        var options = _options.Value;
        var bars = (
            await dataProvider.GetBarsAsync(
                state.Opportunity.Symbol,
                marketOpenUtc,
                DateTimeOffset.UtcNow,
                500,
                cancellationToken
            )
        ).OrderBy(x => x.Timestamp).ToArray();

        if (bars.Length < 6)
        {
            return;
        }

        var openingRange = state.OpeningRange;
        if (openingRange is null)
        {
            if (!strategy.TryBuildOpeningRange(bars, marketOpenUtc, out var builtOpeningRange))
            {
                return;
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
            return;
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
                return;
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
            return;
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
            return;
        }

        var quote = await dataProvider.GetQuoteAsync(state.Opportunity.Symbol, cancellationToken);
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
            return;
        }

        var order = await dataProvider.SubmitBracketOrderAsync(
            new TradingBracketOrderRequest(
                state.Opportunity.Symbol,
                state.Opportunity.Direction,
                options.OrderQuantity,
                tradePlan.StopLossPrice,
                tradePlan.TakeProfitPrice
            ),
            cancellationToken
        );

        state.OrderPlaced = true;
        state.OrderId = order.OrderId;
        state.OrderSubmittedAtUtc = DateTimeOffset.UtcNow;
        state.EntrySignalBarTimestampUtc = retestBar.Timestamp;
        state.PlannedEntryPrice = tradePlan.EntryPrice;
        state.StopLossPrice = tradePlan.StopLossPrice;
        state.TakeProfitPrice = tradePlan.TakeProfitPrice;
        _logger.LogInformation(
            "Order placed for {Symbol}: {Payload}",
            state.Opportunity.Symbol,
            JsonSerializer.Serialize(
                new
                {
                    Symbol = state.Opportunity.Symbol,
                    Direction = state.Opportunity.Direction.ToString(),
                    retestValidation.Score,
                    EntryPrice = tradePlan.EntryPrice,
                    StopLoss = tradePlan.StopLossPrice,
                    TakeProfit = tradePlan.TakeProfitPrice,
                    SignalRetestBarTimestampUtc = retestBar.Timestamp,
                    OrderId = order.OrderId,
                    OrderSubmittedAtUtc = state.OrderSubmittedAtUtc,
                }
            )
        );
    }

    private async Task AuditActiveOrdersAsync(
        ITradingDataProvider dataProvider,
        DateTimeOffset marketOpenUtc,
        CancellationToken cancellationToken
    )
    {
        var pendingAuditStates = _watchStates
            .Values.Where(x => x.OrderPlaced && (!x.EntryAuditLogged || !x.ExitAuditLogged))
            .ToArray();
        if (pendingAuditStates.Length == 0)
        {
            return;
        }

        foreach (var state in pendingAuditStates)
        {
            try
            {
                await AuditOrderLifecycleAsync(
                    dataProvider,
                    state,
                    marketOpenUtc,
                    cancellationToken
                );
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
    }

    private async Task AuditOrderLifecycleAsync(
        ITradingDataProvider dataProvider,
        OpportunityRuntimeState state,
        DateTimeOffset marketOpenUtc,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(state.OrderId))
        {
            return;
        }

        var order = await dataProvider.GetOrderAsync(state.OrderId, cancellationToken);
        if (order is null)
        {
            return;
        }

        if (!state.EntryAuditLogged && order.FilledAt is DateTimeOffset entryFilledAtUtc)
        {
            state.EntryAuditLogged = true;
            state.EntryFilledAtUtc = entryFilledAtUtc;
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
            return;
        }

        var exitLeg = order.Legs
            .Where(x => x.FilledAt is not null)
            .OrderByDescending(x => x.FilledAt)
            .FirstOrDefault();
        if (exitLeg is null)
        {
            return;
        }

        var exitFilledAtUtc = exitLeg.FilledAt!.Value;
        state.ExitAuditLogged = true;
        state.ExitFilledAtUtc = exitFilledAtUtc;
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
                    ExitReason = DetermineExitReasonFromOrderType(exitLeg.OrderType),
                    ExitOrderType = exitLeg.OrderType,
                    StopLoss = state.StopLossPrice,
                    TakeProfit = state.TakeProfitPrice,
                }
            )
        );
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

    private static DateTimeOffset ToMarketDateTimeUtc(DateOnly date, int hour, int minute)
    {
        var local = new DateTime(date.Year, date.Month, date.Day, hour, minute, 0, DateTimeKind.Unspecified);
        var offset = MarketTimeZone.GetUtcOffset(local);
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
    }

    private sealed record LiveTradeBarContext(int BarIndex, DateTimeOffset BarTimestampUtc);
}
