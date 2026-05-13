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
    decimal DirectionalCloseLocation = 0.70m,
    decimal RetestNearRangeFraction = 0.10m,
    decimal RetestPierceRangeFraction = 0.20m,
    decimal RetestBodyToleranceFraction = 0.10m,
    int MaxMinutesBreakoutToRetest = 20,
    decimal BreakoutMinRangeFractionOfOpeningRange = 0.30m,
    int MinCandlesBetweenBreakoutAndRetest = 5,
    decimal BreakoutVolumeMultiplier = 0m,
    decimal RetestOpenToleranceFraction = 0.30m,
    decimal RetestCloseToleranceFraction = 0.05m,
    decimal RetestMaxVolumeFractionOfBreakout = 0m
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

        var openingRangeAverageVolume = ComputeOpeningRangeAverageVolume(openingRange, sessionBars);
        var bars = sessionBars
            .Where(x => x.Timestamp > effectiveSearchStart)
            .OrderBy(x => x.Timestamp)
            .ToArray();

        return bars.FirstOrDefault(x =>
            IsValidatedBreakoutBar(direction, openingRange, x, resolvedThresholds, openingRangeAverageVolume)
        );
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
        var openingRangeAverageVolume = ComputeOpeningRangeAverageVolume(openingRange, sessionBars);
        if (!IsValidatedBreakoutBar(direction, openingRange, breakoutBar, resolvedThresholds, openingRangeAverageVolume))
        {
            return null;
        }

        var maxMinutesBreakoutToRetest = Math.Max(0, resolvedThresholds.MaxMinutesBreakoutToRetest);
        var minCandlesBetweenBreakoutAndRetest = Math.Max(0, resolvedThresholds.MinCandlesBetweenBreakoutAndRetest);

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

            // index - breakoutIndex - 1 is the number of candles strictly between breakout and retest
            if (minCandlesBetweenBreakoutAndRetest > 0 && index - breakoutIndex - 1 < minCandlesBetweenBreakoutAndRetest)
            {
                continue;
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

            if (!IsRetestCandidate(direction, openingRange, retestBar, breakoutBar, resolvedThresholds))
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
        var openingRangeAverageVolume = ComputeOpeningRangeAverageVolume(openingRange, sessionBars);
        if (!IsValidatedBreakoutBar(direction, openingRange, breakoutBar, resolvedThresholds, openingRangeAverageVolume))
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
        decimal rewardToRiskRatio,
        decimal stopLossBufferAsRetestRangeFraction = 0.10m,
        TradingBarSnapshot? breakoutBar = null,
        bool stopAnchorToSwingExtreme = false
    )
    {
        if (entryPrice <= 0m || rewardToRiskRatio <= 0m)
        {
            return null;
        }

        // Stop anchor: by default we anchor at the retest extreme. With
        // stopAnchorToSwingExtreme we anchor at the more conservative of
        // (retestBar, breakoutBar) — the swing low/high of the setup. This
        // respects pre-breakout structure and reduces choppy stop-outs on
        // tight retest candles, at the cost of a wider initial stop.
        var stopAnchorLow = retestBar.Low;
        var stopAnchorHigh = retestBar.High;
        if (stopAnchorToSwingExtreme && breakoutBar is not null)
        {
            stopAnchorLow = Math.Min(stopAnchorLow, breakoutBar.Low);
            stopAnchorHigh = Math.Max(stopAnchorHigh, breakoutBar.High);
        }

        // The stop sits "just under" the swing extreme. The buffer is the larger
        // of two terms:
        //   1. A fraction of the retest candle's own high-low range. This scales
        //      the buffer with realised volatility, so a $5-range bar gets a
        //      proportionally wider buffer than a $0.30-range bar without
        //      penalising high-priced symbols.
        //   2. A small fraction of the anchor price as a safety floor for
        //      degenerate near-flat candles.
        var rangeFraction = Math.Max(0m, stopLossBufferAsRetestRangeFraction);
        var priceFraction = Math.Max(0m, stopLossBufferFraction);
        var retestRange = Math.Max(retestBar.High - retestBar.Low, 0m);

        var bufferAmount = direction switch
        {
            TradingDirection.Bullish => Math.Max(retestRange * rangeFraction, stopAnchorLow * priceFraction),
            TradingDirection.Bearish => Math.Max(retestRange * rangeFraction, stopAnchorHigh * priceFraction),
            _ => 0m,
        };

        var stopLoss = direction switch
        {
            TradingDirection.Bullish => stopAnchorLow - bufferAmount,
            TradingDirection.Bearish => stopAnchorHigh + bufferAmount,
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
        StrategyThresholds thresholds,
        decimal openingRangeAverageVolume
    )
    {
        if (!IsOutsideClose(direction, openingRange, bar))
        {
            return false;
        }

        if (!HasDirectionalClose(direction, bar, thresholds))
        {
            return false;
        }

        if (thresholds.BreakoutMinRangeFractionOfOpeningRange > 0m)
        {
            var openingRangeHeight = Math.Max(openingRange.Upper - openingRange.Lower, 0m);
            var breakoutRange = Math.Max(bar.High - bar.Low, 0m);
            if (
                openingRangeHeight > 0m
                && breakoutRange < openingRangeHeight * thresholds.BreakoutMinRangeFractionOfOpeningRange
            )
            {
                return false;
            }
        }

        // Volume confirmation: real breakouts typically print on expanded volume
        // versus the opening range. A multiplier of 1.5 means the breakout bar
        // must do at least 1.5x the average opening-range volume. We treat
        // missing volume data (or a multiplier of 0) as "not configured" and
        // skip the check so we never silently reject everything when the data
        // feed lacks volume.
        if (thresholds.BreakoutVolumeMultiplier > 0m
            && openingRangeAverageVolume > 0m
            && bar.Volume < openingRangeAverageVolume * thresholds.BreakoutVolumeMultiplier)
        {
            return false;
        }

        return true;
    }

    private static decimal ComputeOpeningRangeAverageVolume(
        OpeningRangeSnapshot openingRange,
        IReadOnlyCollection<TradingBarSnapshot> sessionBars
    )
    {
        var openingBars = sessionBars
            .Where(x => x.Timestamp >= openingRange.StartTime && x.Timestamp <= openingRange.EndTime)
            .ToArray();
        if (openingBars.Length == 0)
        {
            return 0m;
        }

        var totalVolume = 0m;
        var counted = 0;
        foreach (var openingBar in openingBars)
        {
            if (openingBar.Volume <= 0m)
            {
                continue;
            }
            totalVolume += openingBar.Volume;
            counted++;
        }

        return counted > 0 ? totalVolume / counted : 0m;
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
        TradingBarSnapshot breakoutBar,
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
        // Symmetric zone: the open/close strictness used to filter out textbook
        // "spring" retests (open just below the level, wick down, close back
        // above). RetestOpenToleranceFraction widens the open band on the
        // wrong-side; RetestCloseToleranceFraction is intentionally tiny — the
        // close must still confirm the level held.
        var openTolerance = rangeHeight * thresholds.RetestOpenToleranceFraction;
        var closeTolerance = rangeHeight * thresholds.RetestCloseToleranceFraction;

        // Volume filter: real retests typically print on lower volume than the
        // breakout (sellers exhausted). When the multiplier is 0 the check is
        // disabled; missing breakout volume falls back to "skip the check" so
        // we never silently reject every candidate.
        if (thresholds.RetestMaxVolumeFractionOfBreakout > 0m
            && breakoutBar.Volume > 0m
            && bar.Volume > breakoutBar.Volume * thresholds.RetestMaxVolumeFractionOfBreakout)
        {
            return false;
        }

        return direction switch
        {
            TradingDirection.Bullish =>
                bar.Open >= level - openTolerance
                && bar.Low <= level + nearTolerance
                && bar.Low >= level - maxPierce
                && bar.Close >= level - closeTolerance
                && IsRetestDirectionallyAligned(direction, bar, thresholds),
            TradingDirection.Bearish =>
                bar.Open <= level + openTolerance
                && bar.High >= level - nearTolerance
                && bar.High <= level + maxPierce
                && bar.Close <= level + closeTolerance
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
