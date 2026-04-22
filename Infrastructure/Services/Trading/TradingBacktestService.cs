using Application.Features.Trading.Automation;
using Application.Features.Trading.Backtesting;
using Domain.Services.Trading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services.Trading;

public sealed class TradingBacktestService : ITradingBacktestService
{
    private const int MarketCloseHourEt = 16;

    private readonly ITradingDataProvider _dataProvider;
    private readonly ILogger<TradingBacktestService> _logger;
    private readonly IOptions<TradingAutomationOptions> _options;
    private readonly RangeBreakoutRetestStrategy _strategy;
    private readonly ITradingSignalAgent _tradingSignalAgent;

    public TradingBacktestService(
        ITradingDataProvider dataProvider,
        ITradingSignalAgent tradingSignalAgent,
        RangeBreakoutRetestStrategy strategy,
        IOptions<TradingAutomationOptions> options,
        ILogger<TradingBacktestService> logger
    )
    {
        _dataProvider = dataProvider;
        _tradingSignalAgent = tradingSignalAgent;
        _strategy = strategy;
        _options = options;
        _logger = logger;
    }

    public async Task<TradingBacktestResult> RunAsync(
        TradingBacktestRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var calendarDays = Math.Max(0, request.EndDate.DayNumber - request.StartDate.DayNumber + 1);
        var watchlistId = ResolveWatchlistId(request.WatchlistId);
        if (string.IsNullOrWhiteSpace(watchlistId))
        {
            return BuildResult(request, calendarDays, [], []);
        }

        var symbols = await _dataProvider.GetWatchlistSymbolsAsync(watchlistId, cancellationToken);
        if (symbols.Count == 0)
        {
            return BuildResult(request, calendarDays, [], []);
        }

        var maxOpportunities = Math.Clamp(
            request.MaxOpportunities ?? _options.Value.MaxOpportunities,
            1,
            10
        );
        var minimumSentimentScore = Math.Clamp(
            request.MinimumSentimentScore ?? _options.Value.MinimumSentimentScore,
            1,
            100
        );
        var minimumRetestScore = Math.Clamp(
            request.MinimumRetestScore ?? _options.Value.MinimumRetestScore,
            1,
            100
        );

        var tradingTimeZone = ResolveTradingTimeZone(_options.Value.TimeZoneId);
        var dayResults = new List<TradingBacktestDayResult>();
        var tradeResults = new List<TradingBacktestTradeResult>();

        for (var current = request.StartDate; current <= request.EndDate; current = current.AddDays(1))
        {
            if (current.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            {
                continue;
            }

            var opportunities = await GetDailyOpportunitiesAsync(
                symbols,
                maxOpportunities,
                minimumSentimentScore,
                current,
                request.UseAiSentiment,
                cancellationToken
            );
            var dayTrades = new List<TradingBacktestTradeResult>();
            foreach (var opportunity in opportunities)
            {
                try
                {
                    var trade = await SimulateTradeAsync(
                        current,
                        tradingTimeZone,
                        opportunity,
                        minimumRetestScore,
                        request.UseAiRetestValidation,
                        cancellationToken
                    );
                    if (trade is not null)
                    {
                        dayTrades.Add(trade);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Skipping backtest symbol {Symbol} on {Date} due to simulation error.",
                        opportunity.Symbol,
                        current
                    );
                }
            }

            var dayPnl = dayTrades.Sum(x => x.ProfitLoss);
            dayResults.Add(
                new TradingBacktestDayResult(
                    current,
                    opportunities.Count,
                    dayTrades.Count,
                    decimal.Round(dayPnl, 4)
                )
            );
            tradeResults.AddRange(dayTrades);
        }

        return BuildResult(request, calendarDays, dayResults, tradeResults);
    }

    private async Task<IReadOnlyCollection<TradingOpportunity>> GetDailyOpportunitiesAsync(
        IReadOnlyCollection<string> watchlistSymbols,
        int maxOpportunities,
        int minimumSentimentScore,
        DateOnly date,
        bool useAiSentiment,
        CancellationToken cancellationToken
    )
    {
        IReadOnlyCollection<TradingOpportunity> opportunities;
        if (useAiSentiment)
        {
            opportunities = await _tradingSignalAgent.AnalyzeWatchlistSentimentAsync(
                watchlistSymbols,
                maxOpportunities,
                date,
                cancellationToken
            );
        }
        else
        {
            opportunities = watchlistSymbols
                .Take(maxOpportunities)
                .Select((symbol, index) =>
                    new TradingOpportunity(
                        symbol.Trim().ToUpperInvariant(),
                        index % 2 == 0 ? TradingDirection.Bullish : TradingDirection.Bearish,
                        100
                    )
                )
                .ToArray();
        }

        return opportunities
            .Where(x => x.Score >= minimumSentimentScore)
            .OrderByDescending(x => x.Score)
            .Take(maxOpportunities)
            .ToArray();
    }

    private async Task<TradingBacktestTradeResult?> SimulateTradeAsync(
        DateOnly tradingDate,
        TimeZoneInfo tradingTimeZone,
        TradingOpportunity opportunity,
        int minimumRetestScore,
        bool useAiRetestValidation,
        CancellationToken cancellationToken
    )
    {
        var options = _options.Value;
        var marketOpenUtc = ToTradingDateTimeUtc(
            tradingDate,
            Math.Clamp(options.MarketOpenHour, 0, 23),
            Math.Clamp(options.MarketOpenMinute, 0, 59),
            tradingTimeZone
        );
        var marketCloseUtc = ToTradingDateTimeUtc(
            tradingDate,
            MarketCloseHourEt,
            0,
            tradingTimeZone
        );
        var bars = (
            await _dataProvider.GetBarsAsync(
                opportunity.Symbol,
                marketOpenUtc,
                marketCloseUtc,
                1000,
                cancellationToken
            )
        ).OrderBy(x => x.Timestamp).ToArray();

        if (bars.Length < 6)
        {
            return null;
        }

        if (!_strategy.TryBuildOpeningRange(bars, marketOpenUtc, out var openingRange) || openingRange is null)
        {
            return null;
        }

        var breakoutBar = _strategy.FindBreakoutBar(opportunity.Direction, openingRange, bars);
        if (breakoutBar is null)
        {
            return null;
        }

        var retestBar = _strategy.FindRetestBar(
            opportunity.Direction,
            openingRange,
            breakoutBar.Timestamp,
            null,
            bars
        );
        if (retestBar is null)
        {
            return null;
        }

        var retestScore = 100;
        if (useAiRetestValidation)
        {
            var verification = await _tradingSignalAgent.VerifyRetestAsync(
                new RetestVerificationRequest(
                    opportunity.Symbol,
                    opportunity.Direction,
                    openingRange.Upper,
                    openingRange.Lower,
                    breakoutBar,
                    retestBar,
                    bars
                ),
                tradingDate,
                cancellationToken
            );
            retestScore = verification?.Score ?? 0;
        }

        if (retestScore < minimumRetestScore)
        {
            return null;
        }

        var entryPrice = retestBar.Close > 0m ? retestBar.Close : breakoutBar.Close;
        var tradePlan = _strategy.BuildTradePlan(
            opportunity.Direction,
            entryPrice,
            retestBar,
            options.StopLossBufferPercent,
            Math.Max(2m, options.RewardToRiskRatio)
        );
        if (tradePlan is null)
        {
            return null;
        }

        var (exitPrice, exitReason) = ResolveExit(
            opportunity.Direction,
            tradePlan,
            bars.Where(x => x.Timestamp > retestBar.Timestamp).OrderBy(x => x.Timestamp).ToArray()
        );
        var perUnitPnl = CalculatePerUnitPnl(opportunity.Direction, tradePlan.EntryPrice, exitPrice);
        var profitLoss = decimal.Round(perUnitPnl * options.OrderQuantity, 4);
        var rMultiple = tradePlan.RiskPerUnit > 0m
            ? decimal.Round(perUnitPnl / tradePlan.RiskPerUnit, 4)
            : 0m;

        return new TradingBacktestTradeResult(
            tradingDate,
            opportunity.Symbol,
            opportunity.Direction,
            opportunity.Score,
            retestScore,
            decimal.Round(tradePlan.EntryPrice, 4),
            decimal.Round(tradePlan.StopLossPrice, 4),
            decimal.Round(tradePlan.TakeProfitPrice, 4),
            decimal.Round(exitPrice, 4),
            profitLoss,
            rMultiple,
            exitReason
        );
    }

    private static (decimal ExitPrice, string ExitReason) ResolveExit(
        TradingDirection direction,
        TradePlan tradePlan,
        IReadOnlyCollection<TradingBarSnapshot> postEntryBars
    )
    {
        foreach (var bar in postEntryBars)
        {
            var stopHit = direction switch
            {
                TradingDirection.Bullish => bar.Low <= tradePlan.StopLossPrice,
                TradingDirection.Bearish => bar.High >= tradePlan.StopLossPrice,
                _ => false,
            };
            var takeProfitHit = direction switch
            {
                TradingDirection.Bullish => bar.High >= tradePlan.TakeProfitPrice,
                TradingDirection.Bearish => bar.Low <= tradePlan.TakeProfitPrice,
                _ => false,
            };

            if (stopHit && takeProfitHit)
            {
                return (tradePlan.StopLossPrice, "StopLossAndTakeProfitSameBar");
            }

            if (stopHit)
            {
                return (tradePlan.StopLossPrice, "StopLoss");
            }

            if (takeProfitHit)
            {
                return (tradePlan.TakeProfitPrice, "TakeProfit");
            }
        }

        var lastBar = postEntryBars.LastOrDefault();
        if (lastBar is null)
        {
            return (tradePlan.EntryPrice, "NoPostEntryBars");
        }

        return (lastBar.Close, "SessionClose");
    }

    private static decimal CalculatePerUnitPnl(
        TradingDirection direction,
        decimal entryPrice,
        decimal exitPrice
    )
    {
        return direction switch
        {
            TradingDirection.Bullish => exitPrice - entryPrice,
            TradingDirection.Bearish => entryPrice - exitPrice,
            _ => 0m,
        };
    }

    private static TradingBacktestResult BuildResult(
        TradingBacktestRequest request,
        int calendarDays,
        IReadOnlyCollection<TradingBacktestDayResult> dayResults,
        IReadOnlyCollection<TradingBacktestTradeResult> trades
    )
    {
        var totalTrades = trades.Count;
        var wins = trades.Count(x => x.ProfitLoss > 0m);
        var losses = trades.Count(x => x.ProfitLoss < 0m);
        var flatExits = trades.Count(x => x.ProfitLoss == 0m);
        var totalPnl = decimal.Round(trades.Sum(x => x.ProfitLoss), 4);
        var averagePnlPerTrade = totalTrades > 0
            ? decimal.Round(totalPnl / totalTrades, 4)
            : 0m;
        var winRatePercent = totalTrades > 0
            ? decimal.Round((wins * 100m) / totalTrades, 2)
            : 0m;

        return new TradingBacktestResult(
            request.StartDate,
            request.EndDate,
            calendarDays,
            dayResults.Count,
            totalTrades,
            wins,
            losses,
            flatExits,
            totalPnl,
            averagePnlPerTrade,
            winRatePercent,
            dayResults,
            trades
        );
    }

    private string ResolveWatchlistId(string? requestWatchlistId)
    {
        if (!string.IsNullOrWhiteSpace(requestWatchlistId))
        {
            return requestWatchlistId.Trim();
        }

        return _options.Value.WatchlistId.Trim();
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
                // fall through
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

    private static DateTimeOffset ToTradingDateTimeUtc(
        DateOnly date,
        int hour,
        int minute,
        TimeZoneInfo tradingTimeZone
    )
    {
        var local = new DateTime(
            date.Year,
            date.Month,
            date.Day,
            hour,
            minute,
            0,
            DateTimeKind.Unspecified
        );
        var offset = tradingTimeZone.GetUtcOffset(local);
        return new DateTimeOffset(local, offset).ToUniversalTime();
    }
}
