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
    private readonly ITradingCalendarStore _calendarStore;
    private readonly ITradingCandleStore _candleStore;
    private readonly ITradingDataProvider _dataProvider;
    private readonly ILogger<TradingBacktestService> _logger;
    private readonly IOptions<TradingAutomationOptions> _options;
    private readonly ITradingBacktestProgressChannel _progressChannel;
    private readonly RangeBreakoutRetestStrategy _strategy;
    private readonly ITradingSignalAgent _tradingSignalAgent;

    public TradingBacktestService(
        ITradingBacktestCandleCache candleCache,
        ITradingCalendarStore calendarStore,
        ITradingCandleStore candleStore,
        ITradingDataProvider dataProvider,
        ITradingSignalAgent tradingSignalAgent,
        RangeBreakoutRetestStrategy strategy,
        IOptions<TradingAutomationOptions> options,
        ITradingBacktestProgressChannel progressChannel,
        ILogger<TradingBacktestService> logger
    )
    {
        _candleCache = candleCache;
        _calendarStore = calendarStore;
        _candleStore = candleStore;
        _dataProvider = dataProvider;
        _tradingSignalAgent = tradingSignalAgent;
        _strategy = strategy;
        _options = options;
        _progressChannel = progressChannel;
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
        var runId = Guid.NewGuid();

        PublishStarted(runId, request, calendarDays);

        if (string.IsNullOrWhiteSpace(watchlistId))
        {
            PublishCompleted(runId, request, calendarDays, 0, 0, 0m, "Backtest abgeschlossen: keine Watchlist konfiguriert.");
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

        IReadOnlyCollection<string> symbols;
        try
        {
            symbols = await _dataProvider.GetWatchlistSymbolsAsync(watchlistId, cancellationToken);
        }
        catch (Exception ex)
        {
            PublishFailed(runId, request, calendarDays, 0, 0, 0m, ex.Message);
            throw;
        }

        if (symbols.Count == 0)
        {
            PublishCompleted(runId, request, calendarDays, 0, 0, 0m, "Backtest abgeschlossen: Watchlist ist leer.");
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

        await SeedCalendarForRangeAsync(request.StartDate, request.EndDate, cancellationToken);

        var dayResults = new List<TradingBacktestDayResult>();
        var tradeResults = new List<TradingBacktestTradeResult>();
        var simulatedEquity = Math.Max(1m, settings.StartingEquity);
        var processedCalendarDays = 0;
        var cumulativePnl = 0m;

        try
        {
            for (var current = request.StartDate; current <= request.EndDate; current = current.AddDays(1))
            {
                var tradingSession = await _calendarStore.GetSessionAsync(current, cancellationToken);
                processedCalendarDays++;

                if (tradingSession is null)
                {
                    PublishDayProgress(
                        runId,
                        request,
                        calendarDays,
                        processedCalendarDays,
                        current,
                        dayResults.Count,
                        tradeResults.Count,
                        cumulativePnl,
                        lastDayResult: null,
                        lastDayTrades: null,
                        message: $"Kein Handelstag am {current:yyyy-MM-dd}."
                    );
                    continue;
                }

                var opportunities = await GetDailyOpportunitiesAsync(
                    symbols,
                    current,
                    settings,
                    cancellationToken
                );

                var dayTrades = new List<TradingBacktestTradeResult>();
                var dayPnlSoFar = 0m;
                var dayLossCutoff = settings.MaxDailyLossFraction > 0m
                    ? -settings.StartingEquity * settings.MaxDailyLossFraction
                    : (decimal?)null;
                foreach (var opportunity in opportunities)
                {
                    if (settings.MaxTradesPerDay > 0 && dayTrades.Count >= settings.MaxTradesPerDay)
                    {
                        _logger.LogInformation(
                            "Reached MaxTradesPerDay={Max} on {Date}; skipping remaining opportunities.",
                            settings.MaxTradesPerDay,
                            current
                        );
                        break;
                    }
                    if (dayLossCutoff is decimal cutoff && dayPnlSoFar <= cutoff)
                    {
                        _logger.LogInformation(
                            "Daily loss limit hit on {Date} (PnL={Pnl:F2} <= {Cutoff:F2}); halting new entries for the day.",
                            current,
                            dayPnlSoFar,
                            cutoff
                        );
                        break;
                    }

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
                            dayPnlSoFar += trade.ProfitLoss;
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
                var dayResult = new TradingBacktestDayResult(
                    current,
                    opportunities.Count,
                    dayTrades.Count,
                    decimal.Round(dayPnl, 4)
                );
                dayResults.Add(dayResult);
                tradeResults.AddRange(dayTrades);
                cumulativePnl += dayPnl;

                PublishDayProgress(
                    runId,
                    request,
                    calendarDays,
                    processedCalendarDays,
                    current,
                    dayResults.Count,
                    tradeResults.Count,
                    cumulativePnl,
                    lastDayResult: dayResult,
                    lastDayTrades: dayTrades,
                    message: $"{current:yyyy-MM-dd}: {dayTrades.Count} Trades, PnL {decimal.Round(dayPnl, 2)}."
                );
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            PublishFailed(runId, request, calendarDays, processedCalendarDays, tradeResults.Count, cumulativePnl, "Backtest abgebrochen.");
            throw;
        }
        catch (Exception ex)
        {
            PublishFailed(runId, request, calendarDays, processedCalendarDays, tradeResults.Count, cumulativePnl, ex.Message);
            throw;
        }

        PublishCompleted(runId, request, calendarDays, dayResults.Count, tradeResults.Count, cumulativePnl, "Backtest abgeschlossen.");

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

    private async Task<TradingDirection?> ResolvePriorDayDirectionAsync(
        string symbol,
        DateOnly tradingDate,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var dailyInterval = TradingBarIntervalParser.Parse("1d");
            var endUtc = tradingDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var startUtc = endUtc.AddDays(-10);
            var bars = await _dataProvider.GetBarsInRangeAsync(
                symbol,
                dailyInterval,
                new DateTimeOffset(startUtc, TimeSpan.Zero),
                new DateTimeOffset(endUtc, TimeSpan.Zero),
                cancellationToken
            );
            var lastPrior = bars
                .Where(x => x.Timestamp.UtcDateTime.Date < endUtc.Date)
                .OrderByDescending(x => x.Timestamp)
                .FirstOrDefault();
            if (lastPrior is null)
            {
                return null;
            }
            if (lastPrior.Close > lastPrior.Open) return TradingDirection.Bullish;
            if (lastPrior.Close < lastPrior.Open) return TradingDirection.Bearish;
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(
                ex,
                "Backtest prior-day direction lookup failed for {Symbol} {TradingDate}; treating as unknown.",
                symbol,
                tradingDate
            );
            return null;
        }
    }

    private async Task<decimal?> ResolveDailyAtrAsync(
        string symbol,
        DateOnly tradingDate,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var dailyInterval = TradingBarIntervalParser.Parse("1d");
            var endUtc = tradingDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var startUtc = endUtc.AddDays(-60);
            var bars = (await _dataProvider.GetBarsInRangeAsync(
                symbol,
                dailyInterval,
                new DateTimeOffset(startUtc, TimeSpan.Zero),
                new DateTimeOffset(endUtc, TimeSpan.Zero),
                cancellationToken
            ))
                .Where(x => x.Timestamp.UtcDateTime.Date < endUtc.Date)
                .OrderBy(x => x.Timestamp)
                .ToArray();
            const int period = 14;
            if (bars.Length < period + 1)
            {
                return null;
            }
            var trueRanges = new decimal[bars.Length];
            for (var i = 1; i < bars.Length; i++)
            {
                var current = bars[i];
                var previous = bars[i - 1];
                trueRanges[i] = Math.Max(
                    current.High - current.Low,
                    Math.Max(
                        Math.Abs(current.High - previous.Close),
                        Math.Abs(current.Low - previous.Close)
                    )
                );
            }
            var seed = 0m;
            for (var i = 1; i <= period; i++) seed += trueRanges[i];
            var atr = seed / period;
            for (var i = period + 1; i < bars.Length; i++)
            {
                atr = (atr * (period - 1) + trueRanges[i]) / period;
            }
            return atr;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(
                ex,
                "Backtest ATR lookup failed for {Symbol} {TradingDate}; treating as unavailable.",
                symbol,
                tradingDate
            );
            return null;
        }
    }

    private async Task SeedCalendarForRangeAsync(DateOnly start, DateOnly end, CancellationToken cancellationToken)
    {
        var years = Enumerable.Range(start.Year, end.Year - start.Year + 1);
        foreach (var year in years)
        {
            if (await _calendarStore.IsYearSeededAsync(year, cancellationToken))
            {
                continue;
            }

            _logger.LogInformation("Calendar DB: seeding year {Year} from Alpaca API.", year);
            var yearStart = new DateOnly(year, 1, 1);
            var yearEnd = new DateOnly(year, 12, 31);
            var sessions = await _dataProvider.GetTradingSessionsAsync(yearStart, yearEnd, cancellationToken);
            await _calendarStore.SaveSessionsAsync(sessions, cancellationToken);
            _logger.LogInformation("Calendar DB: seeded {Count} trading days for {Year}.", sessions.Count, year);
        }
    }

    private void PublishStarted(Guid runId, TradingBacktestRequest request, int totalCalendarDays)
    {
        _progressChannel.TryPublish(new TradingBacktestProgress(
            runId,
            DateTimeOffset.UtcNow,
            "started",
            request.StartDate,
            request.EndDate,
            totalCalendarDays,
            0,
            0m,
            $"Backtest gestartet ({request.StartDate:yyyy-MM-dd} – {request.EndDate:yyyy-MM-dd})."
        )
        {
            CurrentDate = request.StartDate,
        });
    }

    private void PublishDayProgress(
        Guid runId,
        TradingBacktestRequest request,
        int totalCalendarDays,
        int processedCalendarDays,
        DateOnly currentDate,
        int tradingDaysEvaluated,
        int tradesSoFar,
        decimal cumulativePnl,
        TradingBacktestDayResult? lastDayResult,
        IReadOnlyCollection<TradingBacktestTradeResult>? lastDayTrades,
        string message
    )
    {
        _progressChannel.TryPublish(new TradingBacktestProgress(
            runId,
            DateTimeOffset.UtcNow,
            "running",
            request.StartDate,
            request.EndDate,
            totalCalendarDays,
            processedCalendarDays,
            CalculatePercent(processedCalendarDays, totalCalendarDays),
            message
        )
        {
            CurrentDate = currentDate,
            TradingDaysEvaluated = tradingDaysEvaluated,
            TradesSoFar = tradesSoFar,
            CumulativePnl = decimal.Round(cumulativePnl, 4),
            LastDayResult = lastDayResult,
            LastDayTrades = lastDayTrades,
        });
    }

    private void PublishCompleted(
        Guid runId,
        TradingBacktestRequest request,
        int totalCalendarDays,
        int tradingDaysEvaluated,
        int tradesSoFar,
        decimal cumulativePnl,
        string message
    )
    {
        _progressChannel.TryPublish(new TradingBacktestProgress(
            runId,
            DateTimeOffset.UtcNow,
            "completed",
            request.StartDate,
            request.EndDate,
            totalCalendarDays,
            totalCalendarDays,
            100m,
            message
        )
        {
            CurrentDate = request.EndDate,
            TradingDaysEvaluated = tradingDaysEvaluated,
            TradesSoFar = tradesSoFar,
            CumulativePnl = decimal.Round(cumulativePnl, 4),
        });
    }

    private void PublishFailed(
        Guid runId,
        TradingBacktestRequest request,
        int totalCalendarDays,
        int processedCalendarDays,
        int tradesSoFar,
        decimal cumulativePnl,
        string errorMessage
    )
    {
        _progressChannel.TryPublish(new TradingBacktestProgress(
            runId,
            DateTimeOffset.UtcNow,
            "failed",
            request.StartDate,
            request.EndDate,
            totalCalendarDays,
            processedCalendarDays,
            CalculatePercent(processedCalendarDays, totalCalendarDays),
            "Backtest fehlgeschlagen."
        )
        {
            TradesSoFar = tradesSoFar,
            CumulativePnl = decimal.Round(cumulativePnl, 4),
            ErrorMessage = errorMessage,
        });
    }

    private static decimal CalculatePercent(int processed, int total)
    {
        if (total <= 0)
        {
            return 0m;
        }

        return decimal.Round(Math.Clamp((decimal)processed * 100m / total, 0m, 100m), 2);
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
            if (settings.Direction.HasValue)
            {
                return normalizedWatchlistSymbols
                    .Select(symbol => new TradingOpportunity(symbol, settings.Direction.Value, 100))
                    .ToArray();
            }

            return normalizedWatchlistSymbols
                .SelectMany(symbol => new[]
                {
                    new TradingOpportunity(symbol, TradingDirection.Bullish, 100),
                    new TradingOpportunity(symbol, TradingDirection.Bearish, 100),
                })
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
            async ct =>
            {
                var dbBars = await _candleStore.GetBarsAsync(
                    opportunity.Symbol,
                    tradingSession.OpenTimeUtc,
                    tradingSession.CloseTimeUtc,
                    ct
                );
                if (dbBars.Count > 0)
                    return dbBars;

                var alpacaBars = await _dataProvider.GetBarsAsync(
                    opportunity.Symbol,
                    tradingSession.OpenTimeUtc,
                    tradingSession.CloseTimeUtc,
                    BacktestBarsPerSymbol,
                    ct
                );
                await _candleStore.SaveBarsAsync(opportunity.Symbol, alpacaBars, ct);
                return alpacaBars;
            },
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

        // Gap-day filter: skip days where the first 5-min range is unusually wide
        // relative to price (proxy for gap/news days where the geometry breaks down).
        if (settings.MaxOpeningRangeFractionOfPrice > 0m && openingRange is not null)
        {
            var midpoint = (openingRange.Upper + openingRange.Lower) / 2m;
            if (midpoint > 0m)
            {
                var rangeFraction = (openingRange.Upper - openingRange.Lower) / midpoint;
                if (rangeFraction > settings.MaxOpeningRangeFractionOfPrice)
                {
                    _logger.LogInformation(
                        "Skipping {Symbol} on {Date}: opening-range fraction {Fraction:F4} exceeds gap-day threshold {Threshold:F4}.",
                        opportunity.Symbol,
                        tradingDate,
                        rangeFraction,
                        settings.MaxOpeningRangeFractionOfPrice
                    );
                    return null;
                }
            }
        }

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

        var barsUpToEntry = bars
            .Where(x => x.Timestamp <= retestBar.Timestamp)
            .OrderBy(x => x.Timestamp)
            .ToArray();

        if (!DirectionalIndicatorFilter.IsConfirmed(resolvedDirection, barsUpToEntry, settings.IndicatorSettings))
        {
            _logger.LogInformation(
                "Backtest: skipping {Symbol} {Direction} on {Date} — directional indicators not confirmed (VWAP/EMA).",
                opportunity.Symbol,
                resolvedDirection,
                tradingDate
            );
            return null;
        }

        if (settings.RequirePriorDayDirectionalAlignment)
        {
            var priorDayDirection = await ResolvePriorDayDirectionAsync(
                opportunity.Symbol,
                tradingDate,
                cancellationToken
            );
            if (priorDayDirection is TradingDirection daily && daily != resolvedDirection)
            {
                _logger.LogInformation(
                    "Backtest: skipping {Symbol} {Direction} on {Date} — prior-day direction {PriorDay} disagrees.",
                    opportunity.Symbol,
                    resolvedDirection,
                    tradingDate,
                    daily
                );
                return null;
            }
        }

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
            settings.RewardToRiskRatio,
            settings.StopLossBufferAsRetestRangeFraction,
            breakoutBar: breakoutBar,
            stopAnchorToSwingExtreme: settings.StopAnchorToSwingExtreme
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
            settings.MarketOrderSpreadFillRatio,
            tradePlan.EntryPrice,
            settings.SpreadBpsScaleByPrice
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

        if (settings.MaxStopAtrMultiple > 0m)
        {
            var atr = await ResolveDailyAtrAsync(opportunity.Symbol, tradingDate, cancellationToken);
            if (atr is decimal dailyAtr && dailyAtr > 0m)
            {
                var stopMultiplesOfAtr = effectiveTradePlan.RiskPerUnit / dailyAtr;
                if (stopMultiplesOfAtr > settings.MaxStopAtrMultiple)
                {
                    _logger.LogInformation(
                        "Backtest: skipping {Symbol} on {Date} — stop {Risk} = {Multiple:F2}x ATR exceeds {Threshold:F2}x.",
                        opportunity.Symbol,
                        tradingDate,
                        effectiveTradePlan.RiskPerUnit,
                        stopMultiplesOfAtr,
                        settings.MaxStopAtrMultiple
                    );
                    return null;
                }
            }
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
                settings.TrailingStopBreakEvenProtection,
                settings
            )
            : ResolveExit(resolvedDirection, effectiveTradePlan, postEntryBars, quantity, settings);

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
            exit.ExitReason,
            decimal.Round(exit.MaxFavorableExcursionR, 4),
            decimal.Round(exit.MaxAdverseExcursionR, 4)
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
        decimal quantity,
        BacktestRuntimeSettings settings
    )
    {
        var bars = postEntryBars as TradingBarSnapshot[] ?? postEntryBars.ToArray();
        var stopPrice = tradePlan.StopLossPrice;
        var breakEvenArmed = false;
        var maxFavorable = 0m;
        var maxAdverse = 0m;

        for (var index = 0; index < bars.Length; index++)
        {
            var bar = bars[index];
            UpdateExcursions(direction, tradePlan, bar, ref maxFavorable, ref maxAdverse);

            // Move stop to entry once unrealized PnL has reached the configured R-multiple.
            if (
                !breakEvenArmed
                && settings.BreakEvenAtRMultiple > 0m
                && tradePlan.RiskPerUnit > 0m
            )
            {
                var favorableExtreme = direction switch
                {
                    TradingDirection.Bullish => bar.High,
                    TradingDirection.Bearish => bar.Low,
                    _ => tradePlan.EntryPrice,
                };
                var unrealizedR = direction switch
                {
                    TradingDirection.Bullish => (favorableExtreme - tradePlan.EntryPrice) / tradePlan.RiskPerUnit,
                    TradingDirection.Bearish => (tradePlan.EntryPrice - favorableExtreme) / tradePlan.RiskPerUnit,
                    _ => 0m,
                };
                if (unrealizedR >= settings.BreakEvenAtRMultiple)
                {
                    stopPrice = direction switch
                    {
                        TradingDirection.Bullish => Math.Max(stopPrice, tradePlan.EntryPrice),
                        TradingDirection.Bearish => Math.Min(stopPrice, tradePlan.EntryPrice),
                        _ => stopPrice,
                    };
                    breakEvenArmed = true;
                }
            }

            var stopHit = direction switch
            {
                TradingDirection.Bullish => bar.Low <= stopPrice,
                TradingDirection.Bearish => bar.High >= stopPrice,
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
                var fillPrice = ApplyStopGapSlippage(direction, stopPrice, bar, settings);
                return new ExitSimulation(
                    0m,
                    0m,
                    fillPrice,
                    quantity,
                    null,
                    breakEvenArmed ? "BreakEvenStopAndTakeProfitSameBar" : "StopLossAndTakeProfitSameBar",
                    bar,
                    index + 1,
                    maxFavorable,
                    maxAdverse
                );
            }

            if (stopHit)
            {
                var fillPrice = ApplyStopGapSlippage(direction, stopPrice, bar, settings);
                return new ExitSimulation(
                    0m,
                    0m,
                    fillPrice,
                    quantity,
                    null,
                    breakEvenArmed ? "BreakEvenStop" : "StopLoss",
                    bar,
                    index + 1,
                    maxFavorable,
                    maxAdverse
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
                    index + 1,
                    maxFavorable,
                    maxAdverse
                );
            }

            // Time-stop: force-flat after a configured number of bars without resolution.
            if (
                settings.MaxBarsInTradeBeforeFlatExit > 0
                && index + 1 >= settings.MaxBarsInTradeBeforeFlatExit
            )
            {
                return new ExitSimulation(
                    0m,
                    0m,
                    bar.Close,
                    quantity,
                    null,
                    "TimeStop",
                    bar,
                    index + 1,
                    maxFavorable,
                    maxAdverse
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
                0,
                maxFavorable,
                maxAdverse
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
            bars.Length,
            maxFavorable,
            maxAdverse
        );
    }

    private static void UpdateExcursions(
        TradingDirection direction,
        TradePlan tradePlan,
        TradingBarSnapshot bar,
        ref decimal maxFavorableR,
        ref decimal maxAdverseR
    )
    {
        if (tradePlan.RiskPerUnit <= 0m)
        {
            return;
        }

        var favorablePrice = direction switch
        {
            TradingDirection.Bullish => bar.High,
            TradingDirection.Bearish => bar.Low,
            _ => tradePlan.EntryPrice,
        };
        var adversePrice = direction switch
        {
            TradingDirection.Bullish => bar.Low,
            TradingDirection.Bearish => bar.High,
            _ => tradePlan.EntryPrice,
        };

        var favorableR = direction switch
        {
            TradingDirection.Bullish => (favorablePrice - tradePlan.EntryPrice) / tradePlan.RiskPerUnit,
            TradingDirection.Bearish => (tradePlan.EntryPrice - favorablePrice) / tradePlan.RiskPerUnit,
            _ => 0m,
        };
        var adverseR = direction switch
        {
            TradingDirection.Bullish => (tradePlan.EntryPrice - adversePrice) / tradePlan.RiskPerUnit,
            TradingDirection.Bearish => (adversePrice - tradePlan.EntryPrice) / tradePlan.RiskPerUnit,
            _ => 0m,
        };

        if (favorableR > maxFavorableR)
        {
            maxFavorableR = favorableR;
        }
        if (adverseR > maxAdverseR)
        {
            maxAdverseR = adverseR;
        }
    }

    private static decimal ApplyStopGapSlippage(
        TradingDirection direction,
        decimal stopPrice,
        TradingBarSnapshot bar,
        BacktestRuntimeSettings settings
    )
    {
        if (!settings.StopSlippageOnGap)
        {
            return stopPrice;
        }

        // If the bar opens past the stop, the realistic fill is the worse of the two.
        return direction switch
        {
            TradingDirection.Bullish when bar.Open < stopPrice => bar.Open,
            TradingDirection.Bearish when bar.Open > stopPrice => bar.Open,
            _ => stopPrice,
        };
    }

    private static ExitSimulation ResolveExitWithTrailingStop(
        TradingDirection direction,
        TradePlan tradePlan,
        IReadOnlyCollection<TradingBarSnapshot> postEntryBars,
        PositionSplit split,
        decimal trailingStopRiskMultiple,
        bool useBreakEvenProtection,
        BacktestRuntimeSettings settings
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

        var stopPrice = tradePlan.StopLossPrice;
        var breakEvenArmed = false;
        var maxFavorable = 0m;
        var maxAdverse = 0m;

        for (var index = 0; index < bars.Length; index++)
        {
            var bar = bars[index];
            UpdateExcursions(direction, tradePlan, bar, ref maxFavorable, ref maxAdverse);

            if (
                !breakEvenArmed
                && settings.BreakEvenAtRMultiple > 0m
                && tradePlan.RiskPerUnit > 0m
            )
            {
                var favorableExtreme = direction switch
                {
                    TradingDirection.Bullish => bar.High,
                    TradingDirection.Bearish => bar.Low,
                    _ => tradePlan.EntryPrice,
                };
                var unrealizedR = direction switch
                {
                    TradingDirection.Bullish => (favorableExtreme - tradePlan.EntryPrice) / tradePlan.RiskPerUnit,
                    TradingDirection.Bearish => (tradePlan.EntryPrice - favorableExtreme) / tradePlan.RiskPerUnit,
                    _ => 0m,
                };
                if (unrealizedR >= settings.BreakEvenAtRMultiple)
                {
                    stopPrice = direction switch
                    {
                        TradingDirection.Bullish => Math.Max(stopPrice, tradePlan.EntryPrice),
                        TradingDirection.Bearish => Math.Min(stopPrice, tradePlan.EntryPrice),
                        _ => stopPrice,
                    };
                    breakEvenArmed = true;
                }
            }

            var stopHit = direction switch
            {
                TradingDirection.Bullish => bar.Low <= stopPrice,
                TradingDirection.Bearish => bar.High >= stopPrice,
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
                var fillPrice = ApplyStopGapSlippage(direction, stopPrice, bar, settings);
                return new ExitSimulation(
                    0m,
                    0m,
                    fillPrice,
                    split.TotalQuantity,
                    null,
                    breakEvenArmed ? "BreakEvenStopAndTakeProfitSameBar" : "StopLossAndTakeProfitSameBar",
                    bar,
                    index + 1,
                    maxFavorable,
                    maxAdverse
                );
            }

            if (stopHit)
            {
                var fillPrice = ApplyStopGapSlippage(direction, stopPrice, bar, settings);
                return new ExitSimulation(
                    0m,
                    0m,
                    fillPrice,
                    split.TotalQuantity,
                    null,
                    breakEvenArmed ? "BreakEvenStop" : "StopLoss",
                    bar,
                    index + 1,
                    maxFavorable,
                    maxAdverse
                );
            }

            if (!takeProfitHit)
            {
                if (
                    settings.MaxBarsInTradeBeforeFlatExit > 0
                    && index + 1 >= settings.MaxBarsInTradeBeforeFlatExit
                )
                {
                    return new ExitSimulation(
                        0m,
                        0m,
                        bar.Close,
                        split.TotalQuantity,
                        null,
                        "TimeStop",
                        bar,
                        index + 1,
                        maxFavorable,
                        maxAdverse
                    );
                }
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
                UpdateExcursions(direction, tradePlan, runnerBar, ref maxFavorable, ref maxAdverse);
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
                        runnerIndex + 1,
                        maxFavorable,
                        maxAdverse
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
                bars.Length,
                maxFavorable,
                maxAdverse
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
            bars.Length,
            maxFavorable,
            maxAdverse
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
        decimal spreadFillRatio,
        decimal referencePrice,
        bool scaleByPrice
    )
    {
        var baseSpreadBps = Math.Max(0m, spreadBps);
        var baseSlippageBps = Math.Max(0m, slippageBps);
        var spreadMultiplier = scaleByPrice ? PriceTierMultiplier(referencePrice) : 1m;
        return new TradingExecutionModel(
            (baseSpreadBps * spreadMultiplier) / 10000m,
            (baseSlippageBps * spreadMultiplier) / 10000m,
            Math.Clamp(spreadFillRatio, 0m, 1m)
        );
    }

    /// <summary>
    /// Spread/slippage scaling by price tier. Cheap stocks have wider relative
    /// spreads; mega-caps tighter. Returns a multiplier applied to the configured
    /// baseline bps. Tuned for typical US equity tape:
    /// &lt;$5: 4x, $5-20: 2x, $20-100: 1x, &gt;$100: 0.6x.
    /// </summary>
    private static decimal PriceTierMultiplier(decimal referencePrice)
    {
        if (referencePrice <= 0m)
        {
            return 1m;
        }
        if (referencePrice < 5m) return 4.0m;
        if (referencePrice < 20m) return 2.0m;
        if (referencePrice < 100m) return 1.0m;
        return 0.6m;
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

    private static decimal ResolvePositionQuantity(
        BacktestRuntimeSettings settings,
        decimal accountEquity,
        decimal riskPerUnit
    )
    {
        return PositionSizing.Resolve(
            settings.OrderQuantity,
            accountEquity,
            riskPerUnit,
            settings.RiskPerTradeFraction,
            settings.UseWholeShareQuantity
        );
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
            MaxMinutesBreakoutToRetest: Math.Max(0, request.MaxMinutesBreakoutToRetest ?? options.MaxMinutesBreakoutToRetest),
            BreakoutMinRangeFractionOfOpeningRange: Math.Max(0m, options.BreakoutMinRangeFractionOfOpeningRange),
            MinCandlesBetweenBreakoutAndRetest: Math.Max(0, request.MinCandlesBetweenBreakoutAndRetest ?? options.MinCandlesBetweenBreakoutAndRetest),
            BreakoutVolumeMultiplier: Math.Max(0m, options.BreakoutVolumeMultiplier),
            RetestOpenToleranceFraction: Math.Max(0m, options.RetestOpenToleranceFraction),
            RetestCloseToleranceFraction: Math.Max(0m, options.RetestCloseToleranceFraction),
            RetestMaxVolumeFractionOfBreakout: Math.Max(0m, options.RetestMaxVolumeFractionOfBreakout)
        );

        var indicatorSettings = new DirectionalIndicatorSettings(
            Enabled: request.UseDirectionalIndicatorFilter ?? options.UseDirectionalIndicatorFilter,
            Modes: request.DirectionalIndicatorModes ?? options.DirectionalIndicatorModes,
            RequireAll: request.DirectionalIndicatorRequireAll ?? options.DirectionalIndicatorRequireAll,
            EmaShortPeriod: Math.Max(2, options.DirectionalIndicatorEmaShortPeriod),
            EmaLongPeriod: Math.Max(3, options.DirectionalIndicatorEmaLongPeriod),
            AdxPeriod: Math.Max(2, options.DirectionalIndicatorAdxPeriod),
            AdxThreshold: Math.Max(0m, options.DirectionalIndicatorAdxThreshold),
            SuperTrendAtrPeriod: Math.Max(1, options.DirectionalIndicatorSuperTrendAtrPeriod),
            SuperTrendFactor: Math.Max(0.1m, options.DirectionalIndicatorSuperTrendFactor)
        );

        return new BacktestRuntimeSettings(
            request.UseTrailingStopLoss ?? options.BacktestUseTrailingStopLoss,
            request.UseAiSentiment ?? options.BacktestUseAiSentiment,
            request.Direction,
            request.UseAiRetestValidation ?? options.BacktestUseAiRetestValidationAgent,
            minOpportunities,
            maxOpportunities,
            Math.Clamp(request.MinimumSentimentScore ?? options.MinimumSentimentScore, 1, 100),
            Math.Clamp(request.MinimumRetestScore ?? options.MinimumRetestScore, 1, 100),
            Math.Max(0, request.MinimumMinutesFromMarketOpenForEntry ?? options.MinimumMinutesFromMarketOpenForEntry),
            Math.Max(0, request.MaximumMinutesFromMarketOpenForEntry ?? options.MaximumMinutesFromMarketOpenForEntry),
            Math.Max(0m, request.MinimumEntryDistanceFromRangeFraction ?? options.MinimumEntryDistanceFromRangeFraction),
            request.AllowOppositeDirectionFallback ?? options.BacktestAllowOppositeDirectionFallback,
            Math.Max(1m, request.StartingEquity ?? options.BacktestStartingEquity),
            Math.Max(0m, request.StopLossBufferFraction ?? options.StopLossBufferFraction),
            Math.Max(0m, request.StopLossBufferAsRetestRangeFraction ?? options.StopLossBufferAsRetestRangeFraction),
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
            Math.Max(0m, request.MaxOpeningRangeFractionOfPrice ?? options.MaxOpeningRangeFractionOfPrice),
            Math.Max(0m, request.BreakEvenAtRMultiple ?? options.BreakEvenAtRMultiple),
            Math.Max(0, request.MaxBarsInTradeBeforeFlatExit ?? options.MaxBarsInTradeBeforeFlatExit),
            Math.Max(0, request.MaxTradesPerDay ?? options.MaxTradesPerDay),
            Math.Max(0m, request.MaxDailyLossFraction ?? options.MaxDailyLossFraction),
            request.StopSlippageOnGap ?? options.BacktestStopSlippageOnGap,
            request.SpreadBpsScaleByPrice ?? options.BacktestSpreadBpsScaleByPrice,
            thresholds,
            indicatorSettings,
            options.StopAnchorToSwingExtreme,
            options.RequirePriorDayDirectionalAlignment,
            Math.Max(0m, options.MaxStopAtrMultiple)
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
        int BarsOpen,
        decimal MaxFavorableExcursionR = 0m,
        decimal MaxAdverseExcursionR = 0m
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
        TradingDirection? Direction,
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
        decimal StopLossBufferAsRetestRangeFraction,
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
        decimal MaxOpeningRangeFractionOfPrice,
        decimal BreakEvenAtRMultiple,
        int MaxBarsInTradeBeforeFlatExit,
        int MaxTradesPerDay,
        decimal MaxDailyLossFraction,
        bool StopSlippageOnGap,
        bool SpreadBpsScaleByPrice,
        StrategyThresholds Thresholds,
        DirectionalIndicatorSettings IndicatorSettings,
        bool StopAnchorToSwingExtreme,
        bool RequirePriorDayDirectionalAlignment,
        decimal MaxStopAtrMultiple
    );
}
