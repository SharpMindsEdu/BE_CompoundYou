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

public sealed class RangeBreakoutRetestStrategy
{
    private const decimal DirectionalCloseLocationThreshold = 0.60m;
    private const decimal OppositeDirectionalCloseLocationThreshold = 0.40m;
    private const decimal RetestNearRangeFraction = 0.10m;
    private const decimal RetestPierceRangeFraction = 0.20m;
    private const decimal MaximumPullbackVolumeMultiplier = 1.50m;

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
        DateTimeOffset? searchStartTimestamp = null
    )
    {
        var effectiveSearchStart = openingRange.EndTime;
        if (searchStartTimestamp is DateTimeOffset startTimestamp && startTimestamp > effectiveSearchStart)
        {
            effectiveSearchStart = startTimestamp;
        }

        var bars = sessionBars
            .Where(x => x.Timestamp > effectiveSearchStart)
            .OrderBy(x => x.Timestamp)
            .ToArray();

        return direction switch
        {
            TradingDirection.Bullish => bars.FirstOrDefault(x =>
                IsValidatedBreakoutBar(direction, openingRange, x)
            ),
            TradingDirection.Bearish => bars.FirstOrDefault(x =>
                IsValidatedBreakoutBar(direction, openingRange, x)
            ),
            _ => null,
        };
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

        var breakoutBar = bars[breakoutIndex];
        if (!IsValidatedBreakoutBar(direction, openingRange, breakoutBar))
        {
            return null;
        }

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

            if (HasInvalidatingClose(direction, openingRange, bars, breakoutIndex + 1, index))
            {
                return null;
            }

            if (!HasOutsideAcceptanceBeforeRetest(direction, openingRange, bars, breakoutIndex, index))
            {
                if (ClosesBackInsideRange(direction, openingRange, retestBar))
                {
                    return null;
                }

                continue;
            }

            if (!IsRetestCandidate(direction, openingRange, retestBar))
            {
                if (ClosesBackInsideRange(direction, openingRange, retestBar))
                {
                    return null;
                }

                continue;
            }

            if (index + 1 >= bars.Length)
            {
                return null;
            }

            var confirmationBar = bars[index + 1];
            if (ClosesBackInsideRange(direction, openingRange, confirmationBar))
            {
                return null;
            }

            if (!HasRejectionAndContinuation(direction, openingRange, retestBar, confirmationBar))
            {
                continue;
            }

            if (!HasSupportiveVolume(bars, breakoutIndex, index, retestBar, confirmationBar))
            {
                continue;
            }

            return retestBar;
        }

        return null;
    }

    public TradePlan? BuildTradePlan(
        TradingDirection direction,
        decimal entryPrice,
        TradingBarSnapshot retestBar,
        decimal stopLossBufferPercent,
        decimal rewardToRiskRatio
    )
    {
        if (entryPrice <= 0m || rewardToRiskRatio < 2m)
        {
            return null;
        }

        var bufferMultiplier = stopLossBufferPercent / 100m;
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

    private static bool IsValidatedBreakoutBar(
        TradingDirection direction,
        OpeningRangeSnapshot openingRange,
        TradingBarSnapshot bar
    )
    {
        return IsOutsideClose(direction, openingRange, bar) && HasDirectionalClose(direction, bar);
    }

    private static bool HasOutsideAcceptanceBeforeRetest(
        TradingDirection direction,
        OpeningRangeSnapshot openingRange,
        IReadOnlyList<TradingBarSnapshot> bars,
        int breakoutIndex,
        int retestIndex
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
            if (index > breakoutIndex && HasDirectionalClose(direction, bar))
            {
                continuationCandleSeen = true;
            }
        }

        return outsideCloseCount >= 2 || continuationCandleSeen;
    }

    private static bool IsRetestCandidate(
        TradingDirection direction,
        OpeningRangeSnapshot openingRange,
        TradingBarSnapshot bar
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

        var nearTolerance = ResolveRetestNearTolerance(openingRange, level);
        var maxPierce = ResolveRetestPierceTolerance(openingRange, level);

        return direction switch
        {
            TradingDirection.Bullish =>
                bar.Open >= level
                && bar.Low <= level + nearTolerance
                && bar.Low >= level - maxPierce
                && bar.Close >= level,
            TradingDirection.Bearish =>
                bar.Open <= level
                && bar.High >= level - nearTolerance
                && bar.High <= level + maxPierce
                && bar.Close <= level,
            _ => false,
        };
    }

    private static bool HasRejectionAndContinuation(
        TradingDirection direction,
        OpeningRangeSnapshot openingRange,
        TradingBarSnapshot retestBar,
        TradingBarSnapshot confirmationBar
    )
    {
        if (!IsOutsideClose(direction, openingRange, confirmationBar))
        {
            return false;
        }

        if (HasContinuationConfirmation(direction, retestBar, confirmationBar))
        {
            return true;
        }

        return HasRetestRejection(direction, openingRange, retestBar)
            && HasDirectionalClose(direction, confirmationBar);
    }

    private static bool HasContinuationConfirmation(
        TradingDirection direction,
        TradingBarSnapshot retestBar,
        TradingBarSnapshot confirmationBar
    )
    {
        return direction switch
        {
            TradingDirection.Bullish =>
                confirmationBar.Close > retestBar.High
                || (
                    confirmationBar.Close > retestBar.Close
                    && HasDirectionalClose(direction, confirmationBar)
                ),
            TradingDirection.Bearish =>
                confirmationBar.Close < retestBar.Low
                || (
                    confirmationBar.Close < retestBar.Close
                    && HasDirectionalClose(direction, confirmationBar)
                ),
            _ => false,
        };
    }

    private static bool HasRetestRejection(
        TradingDirection direction,
        OpeningRangeSnapshot openingRange,
        TradingBarSnapshot bar
    )
    {
        var range = bar.High - bar.Low;
        if (range <= 0m)
        {
            return false;
        }

        var level = direction == TradingDirection.Bullish ? openingRange.Upper : openingRange.Lower;
        var body = Math.Abs(bar.Close - bar.Open);
        var lowerWick = Math.Min(bar.Open, bar.Close) - bar.Low;
        var upperWick = bar.High - Math.Max(bar.Open, bar.Close);
        var closeLocation = CloseLocation(bar);

        return direction switch
        {
            TradingDirection.Bullish =>
                bar.Close >= level
                && closeLocation >= DirectionalCloseLocationThreshold
                && lowerWick >= upperWick
                && lowerWick >= body * 0.30m,
            TradingDirection.Bearish =>
                bar.Close <= level
                && closeLocation <= OppositeDirectionalCloseLocationThreshold
                && upperWick >= lowerWick
                && upperWick >= body * 0.30m,
            _ => false,
        };
    }

    private static bool HasSupportiveVolume(
        IReadOnlyList<TradingBarSnapshot> bars,
        int breakoutIndex,
        int retestIndex,
        TradingBarSnapshot retestBar,
        TradingBarSnapshot confirmationBar
    )
    {
        if (retestBar.Volume <= 0m || confirmationBar.Volume <= 0m)
        {
            return false;
        }

        var acceptanceVolumes = new List<decimal>();
        for (var index = breakoutIndex; index < retestIndex; index++)
        {
            if (bars[index].Volume > 0m)
            {
                acceptanceVolumes.Add(bars[index].Volume);
            }
        }

        if (acceptanceVolumes.Count == 0)
        {
            return false;
        }

        var averageAcceptanceVolume = acceptanceVolumes.Average();
        return retestBar.Volume <= averageAcceptanceVolume * MaximumPullbackVolumeMultiplier
            && confirmationBar.Volume >= retestBar.Volume;
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

    private static bool HasDirectionalClose(TradingDirection direction, TradingBarSnapshot bar)
    {
        var closeLocation = CloseLocation(bar);
        return direction switch
        {
            TradingDirection.Bullish =>
                bar.Close > bar.Open && closeLocation >= DirectionalCloseLocationThreshold,
            TradingDirection.Bearish =>
                bar.Close < bar.Open && closeLocation <= OppositeDirectionalCloseLocationThreshold,
            _ => false,
        };
    }

    private static decimal CloseLocation(TradingBarSnapshot bar)
    {
        var range = bar.High - bar.Low;
        return range <= 0m ? 0.5m : (bar.Close - bar.Low) / range;
    }

    private static decimal ResolveRetestNearTolerance(
        OpeningRangeSnapshot openingRange,
        decimal level
    )
    {
        var rangeHeight = Math.Max(openingRange.Upper - openingRange.Lower, 0m);
        return rangeHeight * RetestNearRangeFraction;
    }

    private static decimal ResolveRetestPierceTolerance(
        OpeningRangeSnapshot openingRange,
        decimal level
    )
    {
        var rangeHeight = Math.Max(openingRange.Upper - openingRange.Lower, 0m);
        return rangeHeight * RetestPierceRangeFraction;
    }
}
