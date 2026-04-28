using System.Text.Json;
using Application.Features.Trading.Automation;
using Application.Features.Trading.Backtesting;
using Domain.Services.Trading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services.Trading;

public sealed class TradingBacktestService : ITradingBacktestService
{
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

        var options = _options.Value;
        var maxOpportunities = Math.Clamp(
            request.MaxOpportunities ?? options.MaxOpportunities,
            1,
            20
        );
        var minOpportunities = Math.Clamp(
            request.MinOpportunities ?? options.MinOpportunities,
            1,
            maxOpportunities
        );
        var minimumSentimentScore = Math.Clamp(
            request.MinimumSentimentScore ?? options.MinimumSentimentScore,
            1,
            100
        );
        var minimumRetestScore = Math.Clamp(
            request.MinimumRetestScore ?? options.MinimumRetestScore,
            1,
            100
        );

        var dayResults = new List<TradingBacktestDayResult>();
        var tradeResults = new List<TradingBacktestTradeResult>();
        var simulatedEquity = Math.Max(1m, options.BacktestStartingEquity);

        for (var current = request.StartDate; current <= request.EndDate; current = current.AddDays(1))
        {
            var tradingSession = await _dataProvider.GetTradingSessionAsync(current, cancellationToken);
            if (tradingSession is null)
            {
                continue;
            }

            var opportunities = await GetDailyOpportunitiesAsync(
                symbols,
                maxOpportunities,
                minOpportunities,
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
                        tradingSession,
                        opportunity,
                        minimumRetestScore,
                        request.UseAiSentiment,
                        request.UseAiRetestValidation,
                        simulatedEquity,
                        cancellationToken
                    );
                    if (trade is not null)
                    {
                        dayTrades.Add(trade);
                        simulatedEquity = Math.Max(1m, simulatedEquity + trade.ProfitLoss);
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
        int minOpportunities,
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
                minOpportunities,
                maxOpportunities,
                date,
                cancellationToken
            );
        }
        else
        {
            opportunities = watchlistSymbols
                .Select((symbol, index) =>
                    new TradingOpportunity(
                        symbol.Trim().ToUpperInvariant(),
                        index % 2 == 0 ? TradingDirection.Bullish : TradingDirection.Bearish,
                        100
                    )
                )
                .ToArray();
        }

        var ordered = opportunities
            .OrderByDescending(x => x.Score)
            .ToArray();
        var selected = ordered
            .Where(x => x.Score >= minimumSentimentScore)
            .Take(maxOpportunities)
            .ToList();

        if (selected.Count < minOpportunities)
        {
            var selectedSymbols = selected
                .Select(x => x.Symbol)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var fallback = ordered
                .Where(x => !selectedSymbols.Contains(x.Symbol))
                .Take(minOpportunities - selected.Count);
            selected.AddRange(fallback);
        }

        selected = selected
            .OrderByDescending(x => x.Score)
            .Take(maxOpportunities)
            .ToList();

        if (!useAiSentiment)
        {
            return selected;
        }

        return selected;
    }

    private async Task<TradingBacktestTradeResult?> SimulateTradeAsync(
        DateOnly tradingDate,
        TradingSessionSnapshot tradingSession,
        TradingOpportunity opportunity,
        int minimumRetestScore,
        bool useAiSentiment,
        bool useAiRetestValidation,
        decimal accountEquity,
        CancellationToken cancellationToken
    )
    {
        var options = _options.Value;
        var bars = (
            await _dataProvider.GetBarsAsync(
                opportunity.Symbol,
                tradingSession.OpenTimeUtc,
                tradingSession.CloseTimeUtc,
                1000,
                cancellationToken
            )
        ).OrderBy(x => x.Timestamp).ToArray();

        if (bars.Length < 6)
        {
            return null;
        }

        if (!_strategy.TryBuildOpeningRange(bars, tradingSession.OpenTimeUtc, out var openingRange) || openingRange is null)
        {
            return null;
        }

        openingRange = _strategy.AdjustOpeningRangeForImmediateFailedBreakout(
            opportunity.Direction,
            openingRange,
            bars
        );

        var resolvedSetup = await ResolveSetupAsync(
            tradingDate,
            opportunity.Symbol,
            opportunity.Direction,
            openingRange,
            bars,
            minimumRetestScore,
            useAiSentiment,
            useAiRetestValidation,
            cancellationToken
        );
        if (resolvedSetup is null)
        {
            return null;
        }

        var resolvedDirection = resolvedSetup.Direction;
        var breakoutBar = resolvedSetup.BreakoutBar;
        var retestBar = resolvedSetup.RetestBar;
        var retestScore = resolvedSetup.RetestScore;

        var entryPrice = retestBar.Close > 0m ? retestBar.Close : breakoutBar.Close;
        var tradePlan = _strategy.BuildTradePlan(
            resolvedDirection,
            entryPrice,
            retestBar,
            options.StopLossBufferPercent,
            Math.Max(2m, options.RewardToRiskRatio)
        );
        if (tradePlan is null)
        {
            return null;
        }

        var quantity = ResolveConfiguredOrderQuantity(
            options.OrderQuantity,
            options.UseWholeShareQuantity
        );
        if (quantity <= 0m)
        {
            return null;
        }

        var postEntryBars = bars
            .Where(x => x.Timestamp > retestBar.Timestamp)
            .OrderBy(x => x.Timestamp)
            .ToArray();
        var exit = ResolveExit(resolvedDirection, tradePlan, postEntryBars);

        var executionModel = BuildExecutionModel(
            options.BacktestEstimatedSpreadBps,
            options.BacktestEstimatedSlippageBps
        );
        var adjustedEntryPrice = ApplyEntryExecutionAdjustments(
            resolvedDirection,
            tradePlan.EntryPrice,
            executionModel
        );
        var adjustedExitPrice = ApplyExitExecutionAdjustments(
            resolvedDirection,
            exit.ExitPrice,
            executionModel
        );

        var perUnitPnl = CalculatePerUnitPnl(resolvedDirection, adjustedEntryPrice, adjustedExitPrice);
        var grossProfitLoss = perUnitPnl * quantity;
        var commissions = Math.Max(0m, options.BacktestCommissionPerUnit) * quantity * 2m;
        var profitLoss = decimal.Round(grossProfitLoss - commissions, 4);

        var riskAmount = tradePlan.RiskPerUnit * quantity;
        var rMultiple = riskAmount > 0m
            ? decimal.Round(profitLoss / riskAmount, 4)
            : 0m;

        var entryBarIndex = FindBarIndex(bars, retestBar.Timestamp);
        var exitTimestamp = exit.ExitBar?.Timestamp ?? retestBar.Timestamp;
        var exitBarIndex = exit.ExitBar is null ? entryBarIndex : FindBarIndex(bars, exit.ExitBar.Timestamp);
        var openDuration = exitTimestamp - retestBar.Timestamp;
        if (openDuration < TimeSpan.Zero)
        {
            openDuration = TimeSpan.Zero;
        }

        _logger.LogInformation(
            "Backtest trade entry audit for {Symbol}: {Payload}",
            opportunity.Symbol,
            JsonSerializer.Serialize(
                new
                {
                    Symbol = opportunity.Symbol,
                    Direction = resolvedDirection.ToString(),
                    Date = tradingDate,
                    EntryTimestampUtc = retestBar.Timestamp,
                    EntryBarIndex = entryBarIndex,
                    EntryPrice = decimal.Round(adjustedEntryPrice, 4),
                    StopLoss = decimal.Round(tradePlan.StopLossPrice, 4),
                    TakeProfit = decimal.Round(tradePlan.TakeProfitPrice, 4),
                    Quantity = quantity,
                    AccountEquity = decimal.Round(accountEquity, 2),
                    SentimentScore = opportunity.Score,
                    RetestScore = retestScore,
                    opportunity.SignalInsights,
                }
            )
        );

        _logger.LogInformation(
            "Backtest trade close audit for {Symbol}: {Payload}",
            opportunity.Symbol,
            JsonSerializer.Serialize(
                new
                {
                    Symbol = opportunity.Symbol,
                    Direction = resolvedDirection.ToString(),
                    Date = tradingDate,
                    EntryTimestampUtc = retestBar.Timestamp,
                    EntryBarIndex = entryBarIndex,
                    ExitTimestampUtc = exitTimestamp,
                    ExitBarIndex = exitBarIndex,
                    BarsOpen = exit.BarsOpen,
                    OpenDurationMinutes = decimal.Round((decimal)openDuration.TotalMinutes, 2),
                    ExitPrice = decimal.Round(adjustedExitPrice, 4),
                    exit.ExitReason,
                    GrossProfitLoss = decimal.Round(grossProfitLoss, 4),
                    Commissions = decimal.Round(commissions, 4),
                    ProfitLoss = profitLoss,
                    RMultiple = rMultiple,
                }
            )
        );

        return new TradingBacktestTradeResult(
            tradingDate,
            opportunity.Symbol,
            resolvedDirection,
            opportunity.Score,
            retestScore,
            decimal.Round(adjustedEntryPrice, 4),
            decimal.Round(tradePlan.StopLossPrice, 4),
            decimal.Round(tradePlan.TakeProfitPrice, 4),
            decimal.Round(adjustedExitPrice, 4),
            profitLoss,
            rMultiple,
            exit.ExitReason
        );
    }

    private async Task<ResolvedSetup?> ResolveSetupAsync(
        DateOnly tradingDate,
        string symbol,
        TradingDirection requestedDirection,
        OpeningRangeSnapshot openingRange,
        IReadOnlyCollection<TradingBarSnapshot> bars,
        int minimumRetestScore,
        bool useAiSentiment,
        bool useAiRetestValidation,
        CancellationToken cancellationToken
    )
    {
        var candidateDirections = useAiSentiment
            ? new[] { requestedDirection }
            : new[] { requestedDirection, GetOppositeDirection(requestedDirection) };

        foreach (var direction in candidateDirections)
        {
            DateTimeOffset? breakoutSearchStartTimestamp = null;
            while (true)
            {
                var breakoutBar = _strategy.FindBreakoutBar(
                    direction,
                    openingRange,
                    bars,
                    breakoutSearchStartTimestamp
                );
                if (breakoutBar is null)
                {
                    break;
                }

                DateTimeOffset? lastEvaluatedRetestTimestamp = null;
                while (true)
                {
                    var retestBar = _strategy.FindRetestBar(
                        direction,
                        openingRange,
                        breakoutBar.Timestamp,
                        lastEvaluatedRetestTimestamp,
                        bars
                    );
                    if (retestBar is null)
                    {
                        break;
                    }

                    lastEvaluatedRetestTimestamp = retestBar.Timestamp;
                    var retestScore = 100;
                    if (useAiRetestValidation)
                    {
                        var verification = await _tradingSignalAgent.VerifyRetestAsync(
                            new RetestVerificationRequest(
                                symbol,
                                direction,
                                openingRange.Upper,
                                openingRange.Lower,
                                breakoutBar,
                                retestBar,
                                bars.Where(x => x.Timestamp <= retestBar.Timestamp).ToArray(),
                                EvaluationCutoffTimestampUtc: retestBar.Timestamp
                            ),
                            tradingDate,
                            cancellationToken
                        );
                        retestScore = verification?.Score ?? 0;
                    }

                    if (retestScore >= minimumRetestScore)
                    {
                        return new ResolvedSetup(direction, breakoutBar, retestBar, retestScore);
                    }
                }

                var invalidationBar = _strategy.FindBreakoutInvalidationBar(
                    direction,
                    openingRange,
                    breakoutBar.Timestamp,
                    bars
                );
                if (invalidationBar is null)
                {
                    break;
                }

                breakoutSearchStartTimestamp = invalidationBar.Timestamp;
            }
        }

        return null;
    }

    private static TradingDirection GetOppositeDirection(TradingDirection direction)
    {
        return direction == TradingDirection.Bullish
            ? TradingDirection.Bearish
            : TradingDirection.Bullish;
    }

    private static ExitSimulation ResolveExit(
        TradingDirection direction,
        TradePlan tradePlan,
        IReadOnlyCollection<TradingBarSnapshot> postEntryBars
    )
    {
        var bars = postEntryBars as TradingBarSnapshot[] ?? postEntryBars.ToArray();
        for (var index = 0; index < bars.Length; index++)
        {
            var bar = bars[index];
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
                return new ExitSimulation(
                    tradePlan.StopLossPrice,
                    "StopLossAndTakeProfitSameBar",
                    bar,
                    index + 1
                );
            }

            if (stopHit)
            {
                return new ExitSimulation(tradePlan.StopLossPrice, "StopLoss", bar, index + 1);
            }

            if (takeProfitHit)
            {
                return new ExitSimulation(tradePlan.TakeProfitPrice, "TakeProfit", bar, index + 1);
            }
        }

        var lastBar = bars.LastOrDefault();
        if (lastBar is null)
        {
            return new ExitSimulation(tradePlan.EntryPrice, "NoPostEntryBars", null, 0);
        }

        return new ExitSimulation(lastBar.Close, "SessionClose", lastBar, bars.Length);
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

    private static TradingExecutionModel BuildExecutionModel(decimal spreadBps, decimal slippageBps)
    {
        return new TradingExecutionModel(
            Math.Max(0m, spreadBps) / 10000m,
            Math.Max(0m, slippageBps) / 10000m
        );
    }

    private static decimal ApplyEntryExecutionAdjustments(
        TradingDirection direction,
        decimal rawPrice,
        TradingExecutionModel executionModel
    )
    {
        var halfSpread = rawPrice * executionModel.SpreadRate / 2m;
        var slippage = rawPrice * executionModel.SlippageRate;
        var adjusted = direction switch
        {
            TradingDirection.Bullish => rawPrice + halfSpread + slippage,
            TradingDirection.Bearish => rawPrice - halfSpread - slippage,
            _ => rawPrice,
        };

        return Math.Max(0.0001m, adjusted);
    }

    private static decimal ApplyExitExecutionAdjustments(
        TradingDirection direction,
        decimal rawPrice,
        TradingExecutionModel executionModel
    )
    {
        var halfSpread = rawPrice * executionModel.SpreadRate / 2m;
        var slippage = rawPrice * executionModel.SlippageRate;
        var adjusted = direction switch
        {
            TradingDirection.Bullish => rawPrice - halfSpread - slippage,
            TradingDirection.Bearish => rawPrice + halfSpread + slippage,
            _ => rawPrice,
        };

        return Math.Max(0.0001m, adjusted);
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

    private string ResolveWatchlistId(string? requestWatchlistId)
    {
        if (!string.IsNullOrWhiteSpace(requestWatchlistId))
        {
            return requestWatchlistId.Trim();
        }

        return _options.Value.WatchlistId.Trim();
    }

    private sealed record ExitSimulation(
        decimal ExitPrice,
        string ExitReason,
        TradingBarSnapshot? ExitBar,
        int BarsOpen
    );

    private sealed record ResolvedSetup(
        TradingDirection Direction,
        TradingBarSnapshot BreakoutBar,
        TradingBarSnapshot RetestBar,
        int RetestScore
    );

    private sealed record TradingExecutionModel(decimal SpreadRate, decimal SlippageRate);
}
