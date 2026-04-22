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
        var marketOpenTime = new TimeSpan(
            Math.Clamp(options.MarketOpenHour, 0, 23),
            Math.Clamp(options.MarketOpenMinute, 0, 59),
            0
        );

        if (_lastStateResetDate is null || _lastStateResetDate.Value != tradingDate)
        {
            _watchStates.Clear();
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

        if (_watchStates.Count == 0 || tradingNow.TimeOfDay < marketOpenTime)
        {
            return;
        }

        var marketClock = await dataProvider.GetMarketClockAsync(cancellationToken);
        if (!marketClock.IsOpen)
        {
            return;
        }

        foreach (var state in _watchStates.Values.Where(x => !x.OrderPlaced).ToArray())
        {
            await EvaluateOpportunityAsync(
                strategy,
                dataProvider,
                tradingSignalAgent,
                state,
                tradingDate,
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
        CancellationToken cancellationToken
    )
    {
        var options = _options.Value;
        var marketOpenUtc = ToTradingDateTimeUtc(
            tradingDate,
            Math.Clamp(options.MarketOpenHour, 0, 23),
            Math.Clamp(options.MarketOpenMinute, 0, 59)
        );
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
                    OrderId = order.OrderId,
                }
            )
        );
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

    private DateTimeOffset ToTradingDateTimeUtc(DateOnly date, int hour, int minute)
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
    }
}
