using Domain.Services.Trading;

namespace Application.Features.Trading.Automation;

public sealed record OpeningRangeSnapshot(
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    decimal Upper,
    decimal Lower
);

public sealed record BreakoutRetestSignal(TradingBarSnapshot BreakoutBar, TradingBarSnapshot RetestBar);

public sealed record TradePlan(
    decimal EntryPrice,
    decimal StopLossPrice,
    decimal TakeProfitPrice,
    decimal RiskPerUnit
);

/// <summary>
/// Tunable thresholds for the breakout/retest pattern. All defaults match what
/// used to be hardcoded in <see cref="RangeBreakoutRetestStrategy"/>; pass an
/// override to sweep parameters in backtests.
/// </summary>
public sealed record StrategyThresholds(
    decimal DirectionalCloseLocation = 0.60m,
    decimal RetestNearRangeFraction = 0.10m,
    decimal RetestPierceRangeFraction = 0.20m,
    decimal RetestBodyToleranceFraction = 0.10m,
    int MaxMinutesBreakoutToRetest = 20
)
{
    public decimal OppositeDirectionalCloseLocation => 1m - DirectionalCloseLocation;

    public static StrategyThresholds Default { get; } = new();
}

public sealed class RangeBreakoutRetestStrategy
{
    public bool TryBuildOpeningRange(
        IReadOnlyCollection<TradingBarSnapshot> sessionBars,
        DateTimeOffset marketOpenTime,
        out OpeningRangeSnapshot? openingRange
    )
    {
        openingRange = null;
        if (sessionBars.Count == 0)
        {
            return false;
        }

        var firstFiveBars = sessionBars
            .Where(x => x.Timestamp >= marketOpenTime)
            .OrderBy(x => x.Timestamp)
            .Take(5)
            .ToArray();

        if (firstFiveBars.Length < 5)
        {
            return false;
        }

        openingRange = new OpeningRangeSnapshot(
            firstFiveBars[0].Timestamp,
            firstFiveBars[^1].Timestamp,
            firstFiveBars.Max(x => x.High),
            firstFiveBars.Min(x => x.Low)
        );

        return true;
    }

    public TradingBarSnapshot? FindBreakoutBar(
        TradingDirection direction,
        OpeningRangeSnapshot openingRange,
        IReadOnlyCollection<TradingBarSnapshot> sessionBars,
        DateTimeOffset? searchStartTimestamp = null,
        StrategyThresholds? thresholds = null
    )
    {
        var resolvedThresholds = thresholds ?? StrategyThresholds.Default;
        var effectiveSearchStart = openingRange.EndTime;
        if (searchStartTimestamp is DateTimeOffset startTimestamp && startTimestamp > effectiveSearchStart)
        {
            effectiveSearchStart = startTimestamp;
        }

        var bars = sessionBars
            .Where(x => x.Timestamp > effectiveSearchStart)
            .OrderBy(x => x.Timestamp)
            .ToArray();

        return bars.FirstOrDefault(x => IsValidatedBreakoutBar(direction, openingRange, x, resolvedThresholds));
    }

    public TradingBarSnapshot? FindBreakoutInvalidationBar(
        TradingDirection direction,
        OpeningRangeSnapshot openingRange,
        DateTimeOffset breakoutTimestamp,
        IReadOnlyCollection<TradingBarSnapshot> sessionBars
    )
    {
        var bars = sessionBars
            .Where(x => x.Timestamp > openingRange.EndTime)
            .OrderBy(x => x.Timestamp)
            .ToArray();
        var breakoutIndex = Array.FindIndex(bars, x => x.Timestamp == breakoutTimestamp);
        if (breakoutIndex < 0)
        {
            return null;
        }

        for (var index = breakoutIndex + 1; index < bars.Length; index++)
        {
            if (ClosesBackInsideRange(direction, openingRange, bars[index]))
            {
                return bars[index];
            }
        }

        return null;
    }

    public TradingBarSnapshot? FindRetestBar(
        TradingDirection direction,
        OpeningRangeSnapshot openingRange,
        DateTimeOffset breakoutTimestamp,
        DateTimeOffset? lastEvaluatedRetestTimestamp,
        IReadOnlyCollection<TradingBarSnapshot> sessionBars,
        StrategyThresholds? thresholds = null
    )
    {
        var resolvedThresholds = thresholds ?? StrategyThresholds.Default;
        var bars = sessionBars
            .Where(x => x.Timestamp > openingRange.EndTime)
            .OrderBy(x => x.Timestamp)
            .ToArray();
        var breakoutIndex = Array.FindIndex(bars, x => x.Timestamp == breakoutTimestamp);

        if (breakoutIndex < 0)
        {
            return null;
        }

        var breakoutBar = bars[breakoutIndex];
        if (!IsValidatedBreakoutBar(direction, openingRange, breakoutBar, resolvedThresholds))
        {
            return null;
        }

        var maxMinutesBreakoutToRetest = Math.Max(0, resolvedThresholds.MaxMinutesBreakoutToRetest);

        for (var index = breakoutIndex + 1; index < bars.Length; index++)
        {
            var retestBar = bars[index];
            if (
                lastEvaluatedRetestTimestamp is not null
                && retestBar.Timestamp <= lastEvaluatedRetestTimestamp.Value
            )
            {
                continue;
            }

            if (
                maxMinutesBreakoutToRetest > 0
                && (retestBar.Timestamp - breakoutBar.Timestamp).TotalMinutes > maxMinutesBreakoutToRetest
            )
            {
                return null;
            }

            if (HasInvalidatingClose(direction, openingRange, bars, breakoutIndex + 1, index))
            {
                return null;
            }

            if (!HasOutsideAcceptanceBeforeRetest(direction, openingRange, bars, breakoutIndex, index, resolvedThresholds))
            {
                if (ClosesBackInsideRange(direction, openingRange, retestBar))
                {
                    return null;
                }

                continue;
            }

            if (!IsRetestCandidate(direction, openingRange, retestBar, resolvedThresholds))
            {
                if (ClosesBackInsideRange(direction, openingRange, retestBar))
                {
                    return null;
                }

                continue;
            }

            return retestBar;
        }

        return null;
    }

    public OpeningRangeSnapshot AdjustOpeningRangeForImmediateFailedBreakout(
        TradingDirection direction,
        OpeningRangeSnapshot openingRange,
        IReadOnlyCollection<TradingBarSnapshot> sessionBars,
        StrategyThresholds? thresholds = null
    )
    {
        var resolvedThresholds = thresholds ?? StrategyThresholds.Default;
        if (sessionBars.Count == 0)
        {
            return openingRange;
        }

        var firstSevenBars = sessionBars
            .Where(x => x.Timestamp >= openingRange.StartTime)
            .OrderBy(x => x.Timestamp)
            .Take(7)
            .ToArray();
        if (firstSevenBars.Length < 7)
        {
            return openingRange;
        }

        var breakoutBar = firstSevenBars[5];
        var invalidationBar = firstSevenBars[6];
        if (!IsValidatedBreakoutBar(direction, openingRange, breakoutBar, resolvedThresholds))
        {
            return openingRange;
        }

        if (!ClosesBackInsideRange(direction, openingRange, invalidationBar))
        {
            return openingRange;
        }

        if (!HasImmediateFailureColor(direction, invalidationBar))
        {
            return openingRange;
        }

        var firstSixBars = firstSevenBars.Take(6).ToArray();
        return new OpeningRangeSnapshot(
            firstSixBars[0].Timestamp,
            firstSixBars[^1].Timestamp,
            firstSixBars.Max(x => x.High),
            firstSixBars.Min(x => x.Low)
        );
    }

    public TradePlan? BuildTradePlan(
        TradingDirection direction,
        decimal entryPrice,
        TradingBarSnapshot retestBar,
        decimal stopLossBufferFraction,
        decimal rewardToRiskRatio
    )
    {
        if (entryPrice <= 0m || rewardToRiskRatio <= 0m)
        {
            return null;
        }

        // stopLossBufferFraction is a fraction of price (e.g. 0.005 = 0.5%).
        // Negative values are clamped to zero to avoid flipping the stop.
        var bufferMultiplier = Math.Max(0m, stopLossBufferFraction);
        var stopLoss = direction switch
        {
            TradingDirection.Bullish => retestBar.Low * (1m - bufferMultiplier),
            TradingDirection.Bearish => retestBar.High * (1m + bufferMultiplier),
            _ => 0m,
        };

        if (stopLoss <= 0m)
        {
            return null;
        }

        var riskPerUnit = direction switch
        {
            TradingDirection.Bullish => entryPrice - stopLoss,
            TradingDirection.Bearish => stopLoss - entryPrice,
            _ => 0m,
        };

        if (riskPerUnit <= 0m)
        {
            return null;
        }

        var takeProfit = direction switch
        {
            TradingDirection.Bullish => entryPrice + (riskPerUnit * rewardToRiskRatio),
            TradingDirection.Bearish => entryPrice - (riskPerUnit * rewardToRiskRatio),
            _ => 0m,
        };

        if (takeProfit <= 0m)
        {
            return null;
        }

        return new TradePlan(entryPrice, stopLoss, takeProfit, riskPerUnit);
    }

    public bool MeetsEntryExecutionConstraints(
        TradingDirection direction,
        OpeningRangeSnapshot openingRange,
        TradingBarSnapshot breakoutBar,
        TradingBarSnapshot retestBar,
        DateTimeOffset marketOpenTime,
        int minimumMinutesFromMarketOpen,
        decimal minimumEntryDistanceFromRangeFraction,
        out string rejectionReason,
        int maximumMinutesFromMarketOpen = 0
    )
    {
        rejectionReason = string.Empty;

        var minutesFromMarketOpen = (retestBar.Timestamp - marketOpenTime).TotalMinutes;
        if (minimumMinutesFromMarketOpen > 0 && minutesFromMarketOpen < minimumMinutesFromMarketOpen)
        {
            rejectionReason =
                $"Entry is too early after market open ({minutesFromMarketOpen:F1}m < {minimumMinutesFromMarketOpen}m).";
            return false;
        }

        if (maximumMinutesFromMarketOpen > 0 && minutesFromMarketOpen > maximumMinutesFromMarketOpen)
        {
            rejectionReason =
                $"Entry is too late after market open ({minutesFromMarketOpen:F1}m > {maximumMinutesFromMarketOpen}m).";
            return false;
        }

        var rangeHeight = Math.Max(openingRange.Upper - openingRange.Lower, 0m);
        if (rangeHeight <= 0m)
        {
            rejectionReason = "Opening range height is zero or negative.";
            return false;
        }

        var referenceLevel = direction switch
        {
            TradingDirection.Bullish => openingRange.Upper,
            TradingDirection.Bearish => openingRange.Lower,
            _ => 0m,
        };
        if (referenceLevel <= 0m)
        {
            rejectionReason = "Opening range reference level is not positive.";
            return false;
        }

        var requiredDistanceFraction = Math.Max(0m, minimumEntryDistanceFromRangeFraction);
        if (requiredDistanceFraction <= 0m)
        {
            return true;
        }

        // The "entry distance" check is a quality filter on how far the *breakout*
        // actually pushed away from the level. Using retestBar.Close conflicts with
        // the retest geometry (a retest by definition closes near the level). We
        // measure post-breakout extension instead.
        var extensionPrice = direction switch
        {
            TradingDirection.Bullish => Math.Max(breakoutBar.Close, breakoutBar.High),
            TradingDirection.Bearish => Math.Min(breakoutBar.Close, breakoutBar.Low),
            _ => 0m,
        };
        if (extensionPrice <= 0m)
        {
            rejectionReason = "Breakout extension reference price is not positive.";
            return false;
        }

        var distanceFraction = Math.Abs(extensionPrice - referenceLevel) / rangeHeight;
        if (distanceFraction < requiredDistanceFraction)
        {
            rejectionReason =
                $"Breakout extension from range level is too small ({distanceFraction:F3} < {requiredDistanceFraction:F3}).";
            return false;
        }

        return true;
    }

    private static bool IsValidatedBreakoutBar(
        TradingDirection direction,
        OpeningRangeSnapshot openingRange,
        TradingBarSnapshot bar,
        StrategyThresholds thresholds
    )
    {
        return IsOutsideClose(direction, openingRange, bar)
            && HasDirectionalClose(direction, bar, thresholds);
    }

    private static bool HasOutsideAcceptanceBeforeRetest(
        TradingDirection direction,
        OpeningRangeSnapshot openingRange,
        IReadOnlyList<TradingBarSnapshot> bars,
        int breakoutIndex,
        int retestIndex,
        StrategyThresholds thresholds
    )
    {
        var outsideCloseCount = 0;
        var continuationCandleSeen = false;

        for (var index = breakoutIndex; index < retestIndex; index++)
        {
            var bar = bars[index];
            if (!IsOutsideClose(direction, openingRange, bar))
            {
                continue;
            }

            outsideCloseCount++;
            if (index > breakoutIndex && HasDirectionalClose(direction, bar, thresholds))
            {
                continuationCandleSeen = true;
            }
        }

        return outsideCloseCount >= 2 || continuationCandleSeen;
    }

    private static bool IsRetestCandidate(
        TradingDirection direction,
        OpeningRangeSnapshot openingRange,
        TradingBarSnapshot bar,
        StrategyThresholds thresholds
    )
    {
        var level = direction switch
        {
            TradingDirection.Bullish => openingRange.Upper,
            TradingDirection.Bearish => openingRange.Lower,
            _ => 0m,
        };

        if (level <= 0m)
        {
            return false;
        }

        var rangeHeight = Math.Max(openingRange.Upper - openingRange.Lower, 0m);
        var nearTolerance = rangeHeight * thresholds.RetestNearRangeFraction;
        var maxPierce = rangeHeight * thresholds.RetestPierceRangeFraction;

        return direction switch
        {
            TradingDirection.Bullish =>
                bar.Open >= level
                && bar.Low <= level + nearTolerance
                && bar.Low >= level - maxPierce
                && bar.Close >= level
                && IsRetestDirectionallyAligned(direction, bar, thresholds),
            TradingDirection.Bearish =>
                bar.Open <= level
                && bar.High >= level - nearTolerance
                && bar.High <= level + maxPierce
                && bar.Close <= level
                && IsRetestDirectionallyAligned(direction, bar, thresholds),
            _ => false,
        };
    }

    /// <summary>
    /// Accepts strict directional candles AND small-bodied dojis that still close
    /// on the breakout side of the level. Strict colour-only gating rejects too
    /// many valid wick-rejection retests.
    /// </summary>
    private static bool IsRetestDirectionallyAligned(
        TradingDirection direction,
        TradingBarSnapshot bar,
        StrategyThresholds thresholds
    )
    {
        if (bar.Close == bar.Open)
        {
            return true;
        }

        var range = bar.High - bar.Low;
        var bodyFraction = range > 0m ? Math.Abs(bar.Close - bar.Open) / range : 1m;
        var isSmallBody = bodyFraction <= thresholds.RetestBodyToleranceFraction;

        return direction switch
        {
            TradingDirection.Bullish => bar.Close > bar.Open || isSmallBody,
            TradingDirection.Bearish => bar.Close < bar.Open || isSmallBody,
            _ => false,
        };
    }

    private static bool HasImmediateFailureColor(TradingDirection direction, TradingBarSnapshot bar)
    {
        return direction switch
        {
            TradingDirection.Bullish => bar.Close < bar.Open,
            TradingDirection.Bearish => bar.Close > bar.Open,
            _ => false,
        };
    }

    private static bool HasInvalidatingClose(
        TradingDirection direction,
        OpeningRangeSnapshot openingRange,
        IReadOnlyList<TradingBarSnapshot> bars,
        int startIndex,
        int endIndexExclusive
    )
    {
        for (var index = startIndex; index < endIndexExclusive; index++)
        {
            if (ClosesBackInsideRange(direction, openingRange, bars[index]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsOutsideClose(
        TradingDirection direction,
        OpeningRangeSnapshot openingRange,
        TradingBarSnapshot bar
    )
    {
        return direction switch
        {
            TradingDirection.Bullish => bar.Close > openingRange.Upper,
            TradingDirection.Bearish => bar.Close < openingRange.Lower,
            _ => false,
        };
    }

    private static bool ClosesBackInsideRange(
        TradingDirection direction,
        OpeningRangeSnapshot openingRange,
        TradingBarSnapshot bar
    )
    {
        return direction switch
        {
            TradingDirection.Bullish => bar.Close < openingRange.Upper,
            TradingDirection.Bearish => bar.Close > openingRange.Lower,
            _ => true,
        };
    }

    private static bool HasDirectionalClose(
        TradingDirection direction,
        TradingBarSnapshot bar,
        StrategyThresholds thresholds
    )
    {
        var closeLocation = CloseLocation(bar);
        return direction switch
        {
            TradingDirection.Bullish =>
                bar.Close > bar.Open && closeLocation >= thresholds.DirectionalCloseLocation,
            TradingDirection.Bearish =>
                bar.Close < bar.Open && closeLocation <= thresholds.OppositeDirectionalCloseLocation,
            _ => false,
        };
    }

    private static decimal CloseLocation(TradingBarSnapshot bar)
    {
        var range = bar.High - bar.Low;
        return range <= 0m ? 0.5m : (bar.Close - bar.Low) / range;
    }
}
