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
        IReadOnlyCollection<TradingBarSnapshot> sessionBars
    )
    {
        var bars = sessionBars
            .Where(x => x.Timestamp > openingRange.EndTime)
            .OrderBy(x => x.Timestamp);

        return direction switch
        {
            TradingDirection.Bullish => bars.FirstOrDefault(x => x.Close > openingRange.Upper),
            TradingDirection.Bearish => bars.FirstOrDefault(x => x.Close < openingRange.Lower),
            _ => null,
        };
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
            .Where(x =>
                x.Timestamp > breakoutTimestamp
                && (
                    lastEvaluatedRetestTimestamp is null
                    || x.Timestamp > lastEvaluatedRetestTimestamp.Value
                )
            )
            .OrderBy(x => x.Timestamp);

        return direction switch
        {
            TradingDirection.Bullish => bars.FirstOrDefault(x =>
                x.Low <= openingRange.Upper && x.Close >= openingRange.Upper
            ),
            TradingDirection.Bearish => bars.FirstOrDefault(x =>
                x.High >= openingRange.Lower && x.Close <= openingRange.Lower
            ),
            _ => null,
        };
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
}
