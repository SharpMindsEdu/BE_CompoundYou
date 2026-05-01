using System.Text.Json;
using Application.Features.Trading.Automation;
using Application.Features.Trading.Backtesting;
using Domain.Services.Trading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services.Trading;

public sealed class TradingBacktestService : ITradingBacktestService
{
    private const int BacktestBarsPerSymbol = 1000;

    private readonly ITradingBacktestCandleCache _candleCache;
    private readonly ITradingDataProvider _dataProvider;
    private readonly ILogger<TradingBacktestService> _logger;
    private readonly IOptions<TradingAutomationOptions> _options;
    private readonly RangeBreakoutRetestStrategy _strategy;
    private readonly ITradingSignalAgent _tradingSignalAgent;

    public TradingBacktestService(
        ITradingBacktestCandleCache candleCache,
        ITradingDataProvider dataProvider,
        ITradingSignalAgent tradingSignalAgent,
        RangeBreakoutRetestStrategy strategy,
        IOptions<TradingAutomationOptions> options,
        ILogger<TradingBacktestService> logger
    )
    {
        _candleCache = candleCache;
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
        var settings = ResolveRuntimeSettings(request, _options.Value);
        if (string.IsNullOrWhiteSpace(watchlistId))
        {
            return BuildResult(
                request,
                watchlistId,
                settings.UseTrailingStopLoss,
                settings.UseAiSentiment,
                settings.UseAiRetestValidation,
                calendarDays,
                [],
                []
            );
        }

        var symbols = await _dataProvider.GetWatchlistSymbolsAsync(watchlistId, cancellationToken);
        if (symbols.Count == 0)
        {
            return BuildResult(
                request,
                watchlistId,
                settings.UseTrailingStopLoss,
                settings.UseAiSentiment,
                settings.UseAiRetestValidation,
                calendarDays,
                [],
                []
            );
        }

        var dayResults = new List<TradingBacktestDayResult>();
        var tradeResults = new List<TradingBacktestTradeResult>();
        var simulatedEquity = Math.Max(1m, settings.StartingEquity);

        for (var current = request.StartDate; current <= request.EndDate; current = current.AddDays(1))
        {
            var tradingSession = await _dataProvider.GetTradingSessionAsync(current, cancellationToken);
            if (tradingSession is null)
            {
                continue;
            }

            var opportunities = await GetDailyOpportunitiesAsync(
                symbols,
                current,
                settings,
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
                        settings,
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

        return BuildResult(
            request,
            watchlistId,
            settings.UseTrailingStopLoss,
            settings.UseAiSentiment,
            settings.UseAiRetestValidation,
            calendarDays,
            dayResults,
            tradeResults
        );
    }

    private async Task<IReadOnlyCollection<TradingOpportunity>> GetDailyOpportunitiesAsync(
        IReadOnlyCollection<string> watchlistSymbols,
        DateOnly tradingDate,
        BacktestRuntimeSettings settings,
        CancellationToken cancellationToken
    )
    {
        var normalizedWatchlistSymbols = watchlistSymbols
            .Where(symbol => !string.IsNullOrWhiteSpace(symbol))
            .Select(symbol => symbol.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (!settings.UseAiSentiment)
        {
            return normalizedWatchlistSymbols
                .Select(symbol => new TradingOpportunity(symbol, TradingDirection.Bullish, 100))
                .ToArray();
        }

        if (normalizedWatchlistSymbols.Length == 0)
        {
            return [];
        }

        var aiOpportunities = await _tradingSignalAgent.AnalyzeWatchlistSentimentAsync(
            normalizedWatchlistSymbols,
            settings.MinOpportunities,
            settings.MaxOpportunities,
            tradingDate,
            cancellationToken
        );

        var symbolLookup = normalizedWatchlistSymbols.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return aiOpportunities
            .Where(opportunity =>
                !string.IsNullOrWhiteSpace(opportunity.Symbol)
                && symbolLookup.Contains(opportunity.Symbol)
                && opportunity.Score >= settings.MinimumSentimentScore
            )
            .Select(opportunity => new TradingOpportunity(
                opportunity.Symbol.Trim().ToUpperInvariant(),
                opportunity.Direction,
                Math.Clamp(opportunity.Score, 0, 100),
                opportunity.SignalInsights
            ))
            .DistinctBy(opportunity => opportunity.Symbol, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private async Task<TradingBacktestTradeResult?> SimulateTradeAsync(
        DateOnly tradingDate,
        TradingSessionSnapshot tradingSession,
        TradingOpportunity opportunity,
        BacktestRuntimeSettings settings,
        decimal accountEquity,
        CancellationToken cancellationToken
    )
    {
        var bars = (await _candleCache.GetOrLoadAsync(
            opportunity.Symbol,
            tradingSession.OpenTimeUtc,
            tradingSession.CloseTimeUtc,
            BacktestBarsPerSymbol,
            settings.UseCandleCache,
            async ct => await _dataProvider.GetBarsAsync(
                opportunity.Symbol,
                tradingSession.OpenTimeUtc,
                tradingSession.CloseTimeUtc,
                BacktestBarsPerSymbol,
                ct
            ),
            cancellationToken
        )).OrderBy(x => x.Timestamp).ToArray();

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
            bars,
            settings.Thresholds
        );

        var resolvedSetup = ResolveSetup(
            opportunity.Direction,
            openingRange,
            bars,
            tradingSession.OpenTimeUtc,
            settings.AllowOppositeDirectionFallback,
            settings.MinimumMinutesFromMarketOpenForEntry,
            settings.MaximumMinutesFromMarketOpenForEntry,
            settings.MinimumEntryDistanceFromRangeFraction,
            settings.Thresholds
        );
        if (resolvedSetup is null)
        {
            return null;
        }

        var resolvedDirection = resolvedSetup.Direction;
        var breakoutBar = resolvedSetup.BreakoutBar;
        var retestBar = resolvedSetup.RetestBar;
        var retestScore = resolvedSetup.RetestScore;

        if (settings.UseAiRetestValidation)
        {
            var verification = await _tradingSignalAgent.VerifyRetestAsync(
                new RetestVerificationRequest(
                    opportunity.Symbol,
                    resolvedDirection,
                    openingRange.Upper,
                    openingRange.Lower,
                    breakoutBar,
                    retestBar,
                    bars,
                    retestBar.Timestamp
                ),
                tradingDate,
                cancellationToken
            );

            if (verification is null || !verification.IsValidRetest)
            {
                return null;
            }

            retestScore = Math.Clamp(verification.Score, 0, 100);
        }

        if (retestScore < settings.MinimumRetestScore)
        {
            return null;
        }

        var entryPrice = retestBar.Close > 0m ? retestBar.Close : breakoutBar.Close;
        var tradePlan = _strategy.BuildTradePlan(
            resolvedDirection,
            entryPrice,
            retestBar,
            settings.StopLossBufferFraction,
            settings.RewardToRiskRatio
        );
        if (tradePlan is null)
        {
            return null;
        }

        var quantity = ResolvePositionQuantity(
            settings,
            accountEquity,
            tradePlan.RiskPerUnit
        );
        if (quantity <= 0m)
        {
            return null;
        }

        var executionModel = BuildExecutionModel(
            settings.EstimatedSpreadBps,
            settings.EstimatedSlippageBps,
            settings.MarketOrderSpreadFillRatio
        );
        var adjustedEntryPrice = ApplyEntryExecutionAdjustments(
            resolvedDirection,
            tradePlan.EntryPrice,
            executionModel
        );
        var effectiveTradePlan = RebaseTradePlanAroundEntry(
            resolvedDirection,
            tradePlan,
            adjustedEntryPrice,
            settings.RewardToRiskRatio
        );
        if (effectiveTradePlan is null)
        {
            return null;
        }

        var endOfDayCutoffUtc = ResolveEndOfDayCutoffUtc(
            tradingSession,
            settings.EndOfDayExitBufferMinutes
        );
        var postEntryBars = bars
            .Where(x => x.Timestamp > retestBar.Timestamp)
            .Where(x => endOfDayCutoffUtc is null || x.Timestamp <= endOfDayCutoffUtc.Value)
            .OrderBy(x => x.Timestamp)
            .ToArray();
        var split = ResolvePartialTakeProfitSplit(
            quantity,
            settings.PartialTakeProfitFraction,
            settings.UseWholeShareQuantity
        );
        var exit = settings.UseTrailingStopLoss && split.IsEnabled
            ? ResolveExitWithTrailingStop(
                resolvedDirection,
                effectiveTradePlan,
                postEntryBars,
                split,
                settings.TrailingStopRiskMultiple,
                settings.TrailingStopBreakEvenProtection
            )
            : ResolveExit(resolvedDirection, effectiveTradePlan, postEntryBars, quantity);

        var adjustedPartialExitPrice = exit.PartialTakeProfitQuantity > 0m
            ? ResolveAdjustedExitPrice(
                resolvedDirection,
                exit.PartialTakeProfitExitPrice,
                executionModel,
                exit.ExitReason,
                true
            )
            : 0m;
        var adjustedRunnerExitPrice = ResolveAdjustedExitPrice(
            resolvedDirection,
            exit.RunnerExitPrice,
            executionModel,
            exit.ExitReason,
            false
        );
        var adjustedExitPrice = quantity > 0m
            ? decimal.Round(
                ((adjustedPartialExitPrice * exit.PartialTakeProfitQuantity)
                + (adjustedRunnerExitPrice * exit.RunnerExitQuantity)) / quantity,
                6
            )
            : adjustedRunnerExitPrice;

        var grossProfitLoss =
            (CalculatePerUnitPnl(resolvedDirection, adjustedEntryPrice, adjustedPartialExitPrice) * exit.PartialTakeProfitQuantity)
            + (CalculatePerUnitPnl(resolvedDirection, adjustedEntryPrice, adjustedRunnerExitPrice) * exit.RunnerExitQuantity);
        var commissionFees = Math.Max(0m, settings.CommissionPerUnit) * quantity * 2m;
        var regulatoryFees = CalculateAlpacaStandardFees(
            resolvedDirection,
            adjustedEntryPrice,
            exit.PartialTakeProfitQuantity,
            exit.PartialTakeProfitQuantity > 0m ? adjustedPartialExitPrice : null,
            exit.RunnerExitQuantity,
            adjustedRunnerExitPrice,
            settings
        );
        var commissions = decimal.Round(commissionFees + regulatoryFees, 4);
        var profitLoss = decimal.Round(grossProfitLoss - commissions, 4);

        var riskAmount = effectiveTradePlan.RiskPerUnit * quantity;
        var rMultiple = riskAmount > 0m
            ? decimal.Round(grossProfitLoss / riskAmount, 4)
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
                    StopLoss = decimal.Round(effectiveTradePlan.StopLossPrice, 4),
                    TakeProfit = decimal.Round(effectiveTradePlan.TakeProfitPrice, 4),
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
                    PartialTakeProfitQuantity = decimal.Round(exit.PartialTakeProfitQuantity, 6),
                    RunnerExitQuantity = decimal.Round(exit.RunnerExitQuantity, 6),
                    TrailingStopPrice = exit.TrailingStopPrice.HasValue
                        ? decimal.Round(exit.TrailingStopPrice.Value, 4)
                        : (decimal?)null,
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
            quantity,
            opportunity.Score,
            retestScore,
            breakoutBar.Timestamp,
            retestBar.Timestamp,
            retestBar.Timestamp,
            exitTimestamp,
            entryBarIndex,
            exitBarIndex,
            exit.BarsOpen,
            decimal.Round((decimal)openDuration.TotalMinutes, 2),
            decimal.Round(openingRange.Upper, 4),
            decimal.Round(openingRange.Lower, 4),
            decimal.Round(adjustedEntryPrice, 4),
            decimal.Round(effectiveTradePlan.StopLossPrice, 4),
            decimal.Round(effectiveTradePlan.TakeProfitPrice, 4),
            exit.TrailingStopPrice.HasValue
                ? decimal.Round(exit.TrailingStopPrice.Value, 4)
                : null,
            decimal.Round(exit.PartialTakeProfitQuantity, 6),
            decimal.Round(exit.RunnerExitQuantity, 6),
            decimal.Round(adjustedExitPrice, 4),
            decimal.Round(grossProfitLoss, 4),
            decimal.Round(commissions, 4),
            profitLoss,
            rMultiple,
            exit.ExitReason
        );
    }

    private ResolvedSetup? ResolveSetup(
        TradingDirection requestedDirection,
        OpeningRangeSnapshot openingRange,
        IReadOnlyCollection<TradingBarSnapshot> bars,
        DateTimeOffset marketOpenTimeUtc,
        bool allowOppositeDirectionFallback,
        int minimumMinutesFromMarketOpen,
        int maximumMinutesFromMarketOpen,
        decimal minimumEntryDistanceFromRangeFraction,
        StrategyThresholds thresholds
    )
    {
        var candidateDirections = allowOppositeDirectionFallback
            ? new[] { requestedDirection, GetOppositeDirection(requestedDirection) }
            : new[] { requestedDirection };

        foreach (var direction in candidateDirections)
        {
            DateTimeOffset? breakoutSearchStartTimestamp = null;
            while (true)
            {
                var breakoutBar = _strategy.FindBreakoutBar(
                    direction,
                    openingRange,
                    bars,
                    breakoutSearchStartTimestamp,
                    thresholds
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
                        bars,
                        thresholds
                    );
                    if (retestBar is null)
                    {
                        break;
                    }

                    if (
                        !_strategy.MeetsEntryExecutionConstraints(
                            direction,
                            openingRange,
                            breakoutBar,
                            retestBar,
                            marketOpenTimeUtc,
                            minimumMinutesFromMarketOpen,
                            minimumEntryDistanceFromRangeFraction,
                            out _,
                            maximumMinutesFromMarketOpen
                        )
                    )
                    {
                        lastEvaluatedRetestTimestamp = retestBar.Timestamp;
                        continue;
                    }

                    lastEvaluatedRetestTimestamp = retestBar.Timestamp;
                    const int retestScore = 100;
                    return new ResolvedSetup(direction, breakoutBar, retestBar, retestScore);
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
        IReadOnlyCollection<TradingBarSnapshot> postEntryBars,
        decimal quantity
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
                    0m,
                    0m,
                    tradePlan.StopLossPrice,
                    quantity,
                    null,
                    "StopLossAndTakeProfitSameBar",
                    bar,
                    index + 1
                );
            }

            if (stopHit)
            {
                return new ExitSimulation(
                    0m,
                    0m,
                    tradePlan.StopLossPrice,
                    quantity,
                    null,
                    "StopLoss",
                    bar,
                    index + 1
                );
            }

            if (takeProfitHit)
            {
                return new ExitSimulation(
                    0m,
                    0m,
                    tradePlan.TakeProfitPrice,
                    quantity,
                    null,
                    "TakeProfit",
                    bar,
                    index + 1
                );
            }
        }

        var lastBar = bars.LastOrDefault();
        if (lastBar is null)
        {
            return new ExitSimulation(
                0m,
                0m,
                tradePlan.EntryPrice,
                quantity,
                null,
                "NoPostEntryBars",
                null,
                0
            );
        }

        return new ExitSimulation(
            0m,
            0m,
            lastBar.Close,
            quantity,
            null,
            "SessionClose",
            lastBar,
            bars.Length
        );
    }

    private static ExitSimulation ResolveExitWithTrailingStop(
        TradingDirection direction,
        TradePlan tradePlan,
        IReadOnlyCollection<TradingBarSnapshot> postEntryBars,
        PositionSplit split,
        decimal trailingStopRiskMultiple,
        bool useBreakEvenProtection
    )
    {
        var bars = postEntryBars as TradingBarSnapshot[] ?? postEntryBars.ToArray();
        if (bars.Length == 0)
        {
            return new ExitSimulation(
                0m,
                0m,
                tradePlan.EntryPrice,
                split.TotalQuantity,
                null,
                "NoPostEntryBars",
                null,
                0
            );
        }

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
                    0m,
                    0m,
                    tradePlan.StopLossPrice,
                    split.TotalQuantity,
                    null,
                    "StopLossAndTakeProfitSameBar",
                    bar,
                    index + 1
                );
            }

            if (stopHit)
            {
                return new ExitSimulation(
                    0m,
                    0m,
                    tradePlan.StopLossPrice,
                    split.TotalQuantity,
                    null,
                    "StopLoss",
                    bar,
                    index + 1
                );
            }

            if (!takeProfitHit)
            {
                continue;
            }

            var trailingDistance = Math.Max(0.0001m, tradePlan.RiskPerUnit * Math.Max(0.1m, trailingStopRiskMultiple));
            decimal trailingStop = direction switch
            {
                TradingDirection.Bullish => bar.High - trailingDistance,
                TradingDirection.Bearish => bar.Low + trailingDistance,
                _ => tradePlan.StopLossPrice,
            };

            if (useBreakEvenProtection)
            {
                trailingStop = direction switch
                {
                    TradingDirection.Bullish => Math.Max(trailingStop, tradePlan.EntryPrice),
                    TradingDirection.Bearish => Math.Min(trailingStop, tradePlan.EntryPrice),
                    _ => trailingStop,
                };
            }

            var trailingExtreme = direction switch
            {
                TradingDirection.Bullish => bar.High,
                TradingDirection.Bearish => bar.Low,
                _ => tradePlan.EntryPrice,
            };

            for (var runnerIndex = index + 1; runnerIndex < bars.Length; runnerIndex++)
            {
                var runnerBar = bars[runnerIndex];
                trailingExtreme = direction switch
                {
                    TradingDirection.Bullish => Math.Max(trailingExtreme, runnerBar.High),
                    TradingDirection.Bearish => Math.Min(trailingExtreme, runnerBar.Low),
                    _ => trailingExtreme,
                };

                var nextTrailingStop = direction switch
                {
                    TradingDirection.Bullish => trailingExtreme - trailingDistance,
                    TradingDirection.Bearish => trailingExtreme + trailingDistance,
                    _ => trailingStop,
                };

                if (useBreakEvenProtection)
                {
                    nextTrailingStop = direction switch
                    {
                        TradingDirection.Bullish => Math.Max(nextTrailingStop, tradePlan.EntryPrice),
                        TradingDirection.Bearish => Math.Min(nextTrailingStop, tradePlan.EntryPrice),
                        _ => nextTrailingStop,
                    };
                }

                trailingStop = direction switch
                {
                    TradingDirection.Bullish => Math.Max(trailingStop, nextTrailingStop),
                    TradingDirection.Bearish => Math.Min(trailingStop, nextTrailingStop),
                    _ => trailingStop,
                };

                var trailingStopHit = direction switch
                {
                    TradingDirection.Bullish => runnerBar.Low <= trailingStop,
                    TradingDirection.Bearish => runnerBar.High >= trailingStop,
                    _ => false,
                };

                if (trailingStopHit)
                {
                    return new ExitSimulation(
                        tradePlan.TakeProfitPrice,
                        split.PartialTakeProfitQuantity,
                        trailingStop,
                        split.RunnerQuantity,
                        trailingStop,
                        "TrailingStopAfterTakeProfit",
                        runnerBar,
                        runnerIndex + 1
                    );
                }
            }

            var lastBar = bars[^1];
            return new ExitSimulation(
                tradePlan.TakeProfitPrice,
                split.PartialTakeProfitQuantity,
                lastBar.Close,
                split.RunnerQuantity,
                trailingStop,
                "RunnerSessionCloseAfterTakeProfit",
                lastBar,
                bars.Length
            );
        }

        var sessionCloseBar = bars[^1];
        return new ExitSimulation(
            0m,
            0m,
            sessionCloseBar.Close,
            split.TotalQuantity,
            null,
            "SessionClose",
            sessionCloseBar,
            bars.Length
        );
    }

    private static PositionSplit ResolvePartialTakeProfitSplit(
        decimal quantity,
        decimal configuredFraction,
        bool useWholeShareQuantity
    )
    {
        var totalQuantity = Math.Max(0m, quantity);
        if (totalQuantity <= 0m)
        {
            return PositionSplit.Disabled;
        }

        var fraction = Math.Clamp(configuredFraction, 0.05m, 0.95m);
        var partialTakeProfitQuantity = totalQuantity * fraction;
        if (useWholeShareQuantity)
        {
            partialTakeProfitQuantity = decimal.Floor(partialTakeProfitQuantity);
        }
        else
        {
            partialTakeProfitQuantity = decimal.Round(
                partialTakeProfitQuantity,
                6,
                MidpointRounding.ToZero
            );
        }

        var runnerQuantity = totalQuantity - partialTakeProfitQuantity;
        if (runnerQuantity <= 0m || partialTakeProfitQuantity <= 0m)
        {
            return PositionSplit.Disabled;
        }

        return new PositionSplit(partialTakeProfitQuantity, runnerQuantity, totalQuantity, true);
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

    private static TradingExecutionModel BuildExecutionModel(
        decimal spreadBps,
        decimal slippageBps,
        decimal spreadFillRatio
    )
    {
        return new TradingExecutionModel(
            Math.Max(0m, spreadBps) / 10000m,
            Math.Max(0m, slippageBps) / 10000m,
            Math.Clamp(spreadFillRatio, 0m, 1m)
        );
    }

    private static decimal CalculateAlpacaStandardFees(
        TradingDirection direction,
        decimal adjustedEntryPrice,
        decimal partialExitQuantity,
        decimal? adjustedPartialExitPrice,
        decimal runnerExitQuantity,
        decimal adjustedRunnerExitPrice,
        BacktestRuntimeSettings settings
    )
    {
        if (!settings.UseAlpacaStandardFees)
        {
            return 0m;
        }

        var secPerMillion = Math.Max(0m, settings.AlpacaSecFeePerMillionSold);
        var tafPerShare = Math.Max(0m, settings.AlpacaTafFeePerShareSold);
        var tafMaxPerTrade = Math.Max(0m, settings.AlpacaTafMaxPerTrade);
        var minSellSideFee = Math.Max(0m, settings.AlpacaSellSideMinimumFee);

        var totalFees = 0m;

        if (direction == TradingDirection.Bearish)
        {
            totalFees += CalculateSellSideRegulatoryFees(
                adjustedEntryPrice,
                partialExitQuantity + runnerExitQuantity,
                secPerMillion,
                tafPerShare,
                tafMaxPerTrade,
                minSellSideFee
            );
        }
        else
        {
            if (adjustedPartialExitPrice.HasValue && partialExitQuantity > 0m)
            {
                totalFees += CalculateSellSideRegulatoryFees(
                    adjustedPartialExitPrice.Value,
                    partialExitQuantity,
                    secPerMillion,
                    tafPerShare,
                    tafMaxPerTrade,
                    minSellSideFee
                );
            }

            totalFees += CalculateSellSideRegulatoryFees(
                adjustedRunnerExitPrice,
                runnerExitQuantity,
                secPerMillion,
                tafPerShare,
                tafMaxPerTrade,
                minSellSideFee
            );
        }

        return decimal.Round(totalFees, 4);
    }

    private static decimal CalculateSellSideRegulatoryFees(
        decimal sellPrice,
        decimal quantity,
        decimal secPerMillion,
        decimal tafPerShare,
        decimal tafMaxPerTrade,
        decimal minSellSideFee
    )
    {
        if (quantity <= 0m || sellPrice <= 0m)
        {
            return 0m;
        }

        var sellNotional = sellPrice * quantity;
        var secFee = 0m;
        if (secPerMillion > 0m)
        {
            secFee = RoundUpToCent(sellNotional * (secPerMillion / 1_000_000m));
            if (secFee > 0m && secFee < minSellSideFee)
            {
                secFee = minSellSideFee;
            }
        }

        var tafFee = 0m;
        if (tafPerShare > 0m)
        {
            tafFee = RoundUpToCent(quantity * tafPerShare);
            if (tafMaxPerTrade > 0m)
            {
                tafFee = Math.Min(tafFee, tafMaxPerTrade);
            }

            if (tafFee > 0m && tafFee < minSellSideFee)
            {
                tafFee = minSellSideFee;
            }
        }

        return decimal.Round(secFee + tafFee, 4);
    }

    private static decimal RoundUpToCent(decimal amount)
    {
        if (amount <= 0m)
        {
            return 0m;
        }

        return Math.Ceiling(amount * 100m) / 100m;
    }

    private static decimal ApplyEntryExecutionAdjustments(
        TradingDirection direction,
        decimal rawPrice,
        TradingExecutionModel executionModel
    )
    {
        var halfSpread = rawPrice * executionModel.SpreadRate * executionModel.SpreadFillRatio / 2m;
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
        var halfSpread = rawPrice * executionModel.SpreadRate * executionModel.SpreadFillRatio / 2m;
        var slippage = rawPrice * executionModel.SlippageRate;
        var adjusted = direction switch
        {
            TradingDirection.Bullish => rawPrice - halfSpread - slippage,
            TradingDirection.Bearish => rawPrice + halfSpread + slippage,
            _ => rawPrice,
        };

        return Math.Max(0.0001m, adjusted);
    }

    private static TradePlan? RebaseTradePlanAroundEntry(
        TradingDirection direction,
        TradePlan templatePlan,
        decimal entryPrice,
        decimal rewardToRiskRatio
    )
    {
        if (entryPrice <= 0m || templatePlan.RiskPerUnit <= 0m)
        {
            return null;
        }

        var riskPerUnit = templatePlan.RiskPerUnit;
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

    private static decimal ResolveAdjustedExitPrice(
        TradingDirection direction,
        decimal rawPrice,
        TradingExecutionModel executionModel,
        string exitReason,
        bool isPartialTakeProfitLeg
    )
    {
        if (ShouldUsePlannedExitPrice(exitReason, isPartialTakeProfitLeg))
        {
            return rawPrice;
        }

        return ApplyExitExecutionAdjustments(direction, rawPrice, executionModel);
    }

    private static bool ShouldUsePlannedExitPrice(string exitReason, bool isPartialTakeProfitLeg)
    {
        if (string.Equals(exitReason, "TakeProfit", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (
            string.Equals(exitReason, "StopLoss", StringComparison.OrdinalIgnoreCase)
            || string.Equals(
                exitReason,
                "StopLossAndTakeProfitSameBar",
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return !isPartialTakeProfitLeg;
        }

        if (isPartialTakeProfitLeg)
        {
            return string.Equals(
                exitReason,
                "TrailingStopAfterTakeProfit",
                StringComparison.OrdinalIgnoreCase
            ) || string.Equals(
                exitReason,
                "RunnerSessionCloseAfterTakeProfit",
                StringComparison.OrdinalIgnoreCase
            );
        }

        return false;
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

    private static decimal ResolvePositionQuantity(
        BacktestRuntimeSettings settings,
        decimal accountEquity,
        decimal riskPerUnit
    )
    {
        if (settings.RiskPerTradeFraction > 0m && riskPerUnit > 0m && accountEquity > 0m)
        {
            var dollarRisk = accountEquity * settings.RiskPerTradeFraction;
            var rawQuantity = dollarRisk / riskPerUnit;
            return settings.UseWholeShareQuantity
                ? decimal.Floor(rawQuantity)
                : decimal.Round(rawQuantity, 6, MidpointRounding.ToZero);
        }

        return ResolveConfiguredOrderQuantity(settings.OrderQuantity, settings.UseWholeShareQuantity);
    }

    private static DateTimeOffset? ResolveEndOfDayCutoffUtc(
        TradingSessionSnapshot tradingSession,
        int endOfDayExitBufferMinutes
    )
    {
        if (endOfDayExitBufferMinutes <= 0)
        {
            return null;
        }

        return tradingSession.CloseTimeUtc.AddMinutes(-endOfDayExitBufferMinutes);
    }

    private static BacktestRuntimeSettings ResolveRuntimeSettings(
        TradingBacktestRequest request,
        TradingAutomationOptions options
    )
    {
        var minOpportunities = request.MinOpportunities ?? options.MinOpportunities;
        var maxOpportunities = request.MaxOpportunities ?? options.MaxOpportunities;
        maxOpportunities = Math.Clamp(maxOpportunities, 1, 50);
        minOpportunities = Math.Clamp(minOpportunities, 1, maxOpportunities);

        var thresholds = new StrategyThresholds(
            DirectionalCloseLocation: Math.Clamp(options.BreakoutDirectionalCloseLocationThreshold, 0.5m, 0.95m),
            RetestNearRangeFraction: Math.Max(0m, options.RetestNearRangeFraction),
            RetestPierceRangeFraction: Math.Max(0m, options.RetestPierceRangeFraction),
            RetestBodyToleranceFraction: Math.Clamp(options.RetestBodyToleranceFraction, 0m, 0.5m),
            MaxMinutesBreakoutToRetest: Math.Max(0, options.MaxMinutesBreakoutToRetest)
        );

        return new BacktestRuntimeSettings(
            request.UseTrailingStopLoss ?? options.BacktestUseTrailingStopLoss,
            request.UseAiSentiment ?? options.BacktestUseAiSentiment,
            request.UseAiRetestValidation ?? options.BacktestUseAiRetestValidationAgent,
            minOpportunities,
            maxOpportunities,
            Math.Clamp(request.MinimumSentimentScore ?? options.MinimumSentimentScore, 1, 100),
            Math.Clamp(request.MinimumRetestScore ?? options.MinimumRetestScore, 1, 100),
            Math.Max(0, request.MinimumMinutesFromMarketOpenForEntry ?? options.MinimumMinutesFromMarketOpenForEntry),
            Math.Max(0, options.MaximumMinutesFromMarketOpenForEntry),
            Math.Max(0m, request.MinimumEntryDistanceFromRangeFraction ?? options.MinimumEntryDistanceFromRangeFraction),
            request.AllowOppositeDirectionFallback ?? options.BacktestAllowOppositeDirectionFallback,
            Math.Max(1m, request.StartingEquity ?? options.BacktestStartingEquity),
            Math.Max(0m, request.StopLossBufferFraction ?? options.StopLossBufferFraction),
            Math.Max(0.1m, request.RewardToRiskRatio ?? options.RewardToRiskRatio),
            request.OrderQuantity ?? options.OrderQuantity,
            Math.Max(0m, request.RiskPerTradeFraction ?? options.RiskPerTradeFraction),
            request.UseWholeShareQuantity ?? options.UseWholeShareQuantity,
            Math.Max(0m, request.EstimatedSpreadBps ?? options.BacktestEstimatedSpreadBps),
            Math.Max(0m, request.EstimatedSlippageBps ?? options.BacktestEstimatedSlippageBps),
            Math.Clamp(request.MarketOrderSpreadFillRatio ?? options.BacktestMarketOrderSpreadFillRatio, 0m, 1m),
            Math.Max(0m, request.CommissionPerUnit ?? options.BacktestCommissionPerUnit),
            request.UseAlpacaStandardFees ?? options.BacktestUseAlpacaStandardFees,
            Math.Max(0m, request.PartialTakeProfitFraction ?? options.BacktestPartialTakeProfitFraction),
            Math.Max(0.1m, request.TrailingStopRiskMultiple ?? options.BacktestTrailingStopRiskMultiple),
            request.TrailingStopBreakEvenProtection ?? options.BacktestTrailingStopBreakEvenProtection,
            Math.Max(0m, options.BacktestAlpacaSecFeePerMillionSold),
            Math.Max(0m, options.BacktestAlpacaTafFeePerShareSold),
            Math.Max(0m, options.BacktestAlpacaTafMaxPerTrade),
            Math.Max(0m, options.BacktestAlpacaSellSideMinimumFee),
            request.UseCandleCache ?? options.BacktestCandleCacheEnabled,
            Math.Max(0, options.EndOfDayExitBufferMinutes),
            thresholds
        );
    }

    private static TradingBacktestResult BuildResult(
        TradingBacktestRequest request,
        string watchlistId,
        bool useTrailingStopLoss,
        bool useAiSentiment,
        bool useAiRetestValidation,
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
            watchlistId,
            useTrailingStopLoss,
            useAiSentiment,
            useAiRetestValidation,
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
        decimal PartialTakeProfitExitPrice,
        decimal PartialTakeProfitQuantity,
        decimal RunnerExitPrice,
        decimal RunnerExitQuantity,
        decimal? TrailingStopPrice,
        string ExitReason,
        TradingBarSnapshot? ExitBar,
        int BarsOpen
    )
    {
        public decimal ExitPrice => RunnerExitPrice;
    }

    private sealed record PositionSplit(
        decimal PartialTakeProfitQuantity,
        decimal RunnerQuantity,
        decimal TotalQuantity,
        bool IsEnabled
    )
    {
        public static PositionSplit Disabled { get; } = new(0m, 0m, 0m, false);
    }

    private sealed record ResolvedSetup(
        TradingDirection Direction,
        TradingBarSnapshot BreakoutBar,
        TradingBarSnapshot RetestBar,
        int RetestScore
    );

    private sealed record TradingExecutionModel(
        decimal SpreadRate,
        decimal SlippageRate,
        decimal SpreadFillRatio
    );

    private sealed record BacktestRuntimeSettings(
        bool UseTrailingStopLoss,
        bool UseAiSentiment,
        bool UseAiRetestValidation,
        int MinOpportunities,
        int MaxOpportunities,
        int MinimumSentimentScore,
        int MinimumRetestScore,
        int MinimumMinutesFromMarketOpenForEntry,
        int MaximumMinutesFromMarketOpenForEntry,
        decimal MinimumEntryDistanceFromRangeFraction,
        bool AllowOppositeDirectionFallback,
        decimal StartingEquity,
        decimal StopLossBufferFraction,
        decimal RewardToRiskRatio,
        decimal OrderQuantity,
        decimal RiskPerTradeFraction,
        bool UseWholeShareQuantity,
        decimal EstimatedSpreadBps,
        decimal EstimatedSlippageBps,
        decimal MarketOrderSpreadFillRatio,
        decimal CommissionPerUnit,
        bool UseAlpacaStandardFees,
        decimal PartialTakeProfitFraction,
        decimal TrailingStopRiskMultiple,
        bool TrailingStopBreakEvenProtection,
        decimal AlpacaSecFeePerMillionSold,
        decimal AlpacaTafFeePerShareSold,
        decimal AlpacaTafMaxPerTrade,
        decimal AlpacaSellSideMinimumFee,
        bool UseCandleCache,
        int EndOfDayExitBufferMinutes,
        StrategyThresholds Thresholds
    );
}
