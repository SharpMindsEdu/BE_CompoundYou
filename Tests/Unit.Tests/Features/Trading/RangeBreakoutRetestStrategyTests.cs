using Application.Features.Trading.Automation;
using Domain.Services.Trading;

namespace Unit.Tests.Features.Trading;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.TradingTests)]
public sealed class RangeBreakoutRetestStrategyTests
{
    private readonly RangeBreakoutRetestStrategy _strategy = new();

    [Fact]
    public void TryBuildOpeningRange_WithFiveBars_BuildsExpectedRange()
    {
        var open = new DateTimeOffset(2026, 04, 21, 13, 30, 0, TimeSpan.Zero);
        var bars = BuildBars(
            "SPY",
            open,
            [
                (100m, 101m, 99m, 100.5m),
                (100.5m, 101.4m, 100.1m, 101m),
                (101m, 101.2m, 100.2m, 100.7m),
                (100.7m, 101.8m, 100.6m, 101.5m),
                (101.5m, 102.2m, 101.1m, 102m),
                (102m, 102.5m, 101.8m, 102.3m),
            ]
        );

        var success = _strategy.TryBuildOpeningRange(bars, open, out var openingRange);

        Assert.True(success);
        Assert.NotNull(openingRange);
        Assert.Equal(102.2m, openingRange!.Upper);
        Assert.Equal(99m, openingRange.Lower);
    }

    [Fact]
    public void FindBreakoutBar_Bullish_ReturnsFirstCloseAboveRange()
    {
        var open = new DateTimeOffset(2026, 04, 21, 13, 30, 0, TimeSpan.Zero);
        var bars = BuildBars(
            "SPY",
            open,
            [
                (100m, 101m, 99m, 100.5m),
                (100.5m, 101m, 99.9m, 100.4m),
                (100.4m, 101m, 100m, 100.6m),
                (100.6m, 101.2m, 100.2m, 100.8m),
                (100.8m, 101.5m, 100.5m, 101m),
                (101m, 101.6m, 100.8m, 101.3m),
                (101.3m, 102m, 101.1m, 101.9m),
            ]
        );

        _strategy.TryBuildOpeningRange(bars, open, out var openingRange);
        var breakout = _strategy.FindBreakoutBar(TradingDirection.Bullish, openingRange!, bars);

        Assert.NotNull(breakout);
        Assert.Equal(101.9m, breakout!.Close);
    }

    [Fact]
    public void FindBreakoutBar_Bullish_SkipsWickOnlyBreakout()
    {
        var open = new DateTimeOffset(2026, 04, 21, 13, 30, 0, TimeSpan.Zero);
        var bars = BuildBars(
            "SPY",
            open,
            [
                (100m, 101m, 99m, 100.5m),
                (100.5m, 101m, 99.9m, 100.4m),
                (100.4m, 101m, 100m, 100.6m),
                (100.6m, 101.2m, 100.2m, 100.8m),
                (100.8m, 101.5m, 100.5m, 101m),
                (101m, 102m, 100.8m, 101.2m),
                (101.2m, 102.1m, 101.1m, 101.9m),
            ]
        );

        _strategy.TryBuildOpeningRange(bars, open, out var openingRange);
        var breakout = _strategy.FindBreakoutBar(TradingDirection.Bullish, openingRange!, bars);

        Assert.NotNull(breakout);
        Assert.Equal(open.AddMinutes(6), breakout!.Timestamp);
    }

    [Fact]
    public void FindRetestBar_Bullish_ReturnsRetestAfterAcceptanceAndConfirmation()
    {
        var open = new DateTimeOffset(2026, 04, 21, 13, 30, 0, TimeSpan.Zero);
        var bars = BuildBars(
            "SPY",
            open,
            [
                (100m, 101m, 99m, 100.5m),
                (100.5m, 101m, 99.9m, 100.4m),
                (100.4m, 101m, 100m, 100.6m),
                (100.6m, 101.2m, 100.2m, 100.8m),
                (100.8m, 101.5m, 100.5m, 101m),
                (101m, 101.9m, 100.9m, 101.8m),
                (101.8m, 102.2m, 101.6m, 102.1m),
                (102.1m, 102.2m, 101.35m, 101.85m),
                (101.85m, 102.5m, 101.8m, 102.45m),
            ]
        );

        _strategy.TryBuildOpeningRange(bars, open, out var openingRange);
        var breakout = _strategy.FindBreakoutBar(TradingDirection.Bullish, openingRange!, bars);
        var retest = _strategy.FindRetestBar(
            TradingDirection.Bullish,
            openingRange!,
            breakout!.Timestamp,
            null,
            bars
        );

        Assert.NotNull(retest);
        Assert.Equal(open.AddMinutes(7), retest!.Timestamp);
    }

    [Fact]
    public void FindRetestBar_Bullish_RejectsImmediateRetestWithoutAcceptance()
    {
        var open = new DateTimeOffset(2026, 04, 21, 13, 30, 0, TimeSpan.Zero);
        var bars = BuildBars(
            "SPY",
            open,
            [
                (100m, 101m, 99m, 100.5m),
                (100.5m, 101m, 99.9m, 100.4m),
                (100.4m, 101m, 100m, 100.6m),
                (100.6m, 101.2m, 100.2m, 100.8m),
                (100.8m, 101.5m, 100.5m, 101m),
                (101m, 101.9m, 100.9m, 101.8m),
                (101.8m, 101.9m, 101.35m, 101.75m),
                (101.75m, 102.2m, 101.7m, 102.1m),
            ]
        );

        _strategy.TryBuildOpeningRange(bars, open, out var openingRange);
        var breakout = _strategy.FindBreakoutBar(TradingDirection.Bullish, openingRange!, bars);
        var retest = _strategy.FindRetestBar(
            TradingDirection.Bullish,
            openingRange!,
            breakout!.Timestamp,
            null,
            bars
        );

        Assert.Null(retest);
    }

    [Fact]
    public void FindRetestBar_Bullish_RejectsCloseBackInsideRangeBeforeConfirmation()
    {
        var open = new DateTimeOffset(2026, 04, 21, 13, 30, 0, TimeSpan.Zero);
        var bars = BuildBars(
            "SPY",
            open,
            [
                (100m, 101m, 99m, 100.5m),
                (100.5m, 101m, 99.9m, 100.4m),
                (100.4m, 101m, 100m, 100.6m),
                (100.6m, 101.2m, 100.2m, 100.8m),
                (100.8m, 101.5m, 100.5m, 101m),
                (101m, 101.9m, 100.9m, 101.8m),
                (101.8m, 102.2m, 101.6m, 102.1m),
                (102.1m, 102.2m, 101.35m, 101.85m),
                (101.85m, 101.95m, 101.2m, 101.4m),
            ]
        );

        _strategy.TryBuildOpeningRange(bars, open, out var openingRange);
        var breakout = _strategy.FindBreakoutBar(TradingDirection.Bullish, openingRange!, bars);
        var retest = _strategy.FindRetestBar(
            TradingDirection.Bullish,
            openingRange!,
            breakout!.Timestamp,
            null,
            bars
        );

        Assert.Null(retest);
    }

    [Fact]
    public void FindRetestBar_Bullish_RejectsDeepPierceThroughBrokenLevel()
    {
        var open = new DateTimeOffset(2026, 04, 21, 13, 30, 0, TimeSpan.Zero);
        var bars = BuildBars(
            "SPY",
            open,
            [
                (100m, 101m, 99m, 100.5m),
                (100.5m, 101m, 99.9m, 100.4m),
                (100.4m, 101m, 100m, 100.6m),
                (100.6m, 101.2m, 100.2m, 100.8m),
                (100.8m, 101.5m, 100.5m, 101m),
                (101m, 101.9m, 100.9m, 101.8m),
                (101.8m, 102.2m, 101.6m, 102.1m),
                (102.1m, 102.2m, 100.9m, 101.85m),
                (101.85m, 102.5m, 101.8m, 102.45m),
            ]
        );

        _strategy.TryBuildOpeningRange(bars, open, out var openingRange);
        var breakout = _strategy.FindBreakoutBar(TradingDirection.Bullish, openingRange!, bars);
        var retest = _strategy.FindRetestBar(
            TradingDirection.Bullish,
            openingRange!,
            breakout!.Timestamp,
            null,
            bars
        );

        Assert.Null(retest);
    }

    [Fact]
    public void FindRetestBar_Bullish_RejectsWeakContinuationVolume()
    {
        var open = new DateTimeOffset(2026, 04, 21, 13, 30, 0, TimeSpan.Zero);
        var bars = BuildBars(
            "SPY",
            open,
            [
                (100m, 101m, 99m, 100.5m, 1000m),
                (100.5m, 101m, 99.9m, 100.4m, 1000m),
                (100.4m, 101m, 100m, 100.6m, 1000m),
                (100.6m, 101.2m, 100.2m, 100.8m, 1000m),
                (100.8m, 101.5m, 100.5m, 101m, 1000m),
                (101m, 101.9m, 100.9m, 101.8m, 1500m),
                (101.8m, 102.2m, 101.6m, 102.1m, 1400m),
                (102.1m, 102.2m, 101.35m, 101.85m, 1000m),
                (101.85m, 102.5m, 101.8m, 102.45m, 800m),
            ]
        );

        _strategy.TryBuildOpeningRange(bars, open, out var openingRange);
        var breakout = _strategy.FindBreakoutBar(TradingDirection.Bullish, openingRange!, bars);
        var retest = _strategy.FindRetestBar(
            TradingDirection.Bullish,
            openingRange!,
            breakout!.Timestamp,
            null,
            bars
        );

        Assert.Null(retest);
    }

    [Fact]
    public void FindRetestBar_Bearish_SkipsAlreadyEvaluatedBars()
    {
        var open = new DateTimeOffset(2026, 04, 21, 13, 30, 0, TimeSpan.Zero);
        var bars = BuildBars(
            "QQQ",
            open,
            [
                (200m, 201m, 199.5m, 200.2m),
                (200.2m, 201.2m, 199.8m, 200.6m),
                (200.6m, 201m, 199.7m, 200m),
                (200m, 200.8m, 199.4m, 199.6m),
                (199.6m, 200.5m, 199.2m, 199.4m),
                (199.4m, 199.5m, 198.8m, 199m),
                (199m, 199m, 198.4m, 198.7m),
                (198.7m, 199.35m, 198.55m, 198.85m),
                (198.85m, 198.95m, 198.2m, 198.3m),
                (198.3m, 199.3m, 198.1m, 198.6m),
                (198.6m, 198.75m, 197.9m, 198m),
            ]
        );

        _strategy.TryBuildOpeningRange(bars, open, out var openingRange);
        var breakout = _strategy.FindBreakoutBar(TradingDirection.Bearish, openingRange!, bars);
        var firstRetest = _strategy.FindRetestBar(
            TradingDirection.Bearish,
            openingRange!,
            breakout!.Timestamp,
            null,
            bars
        );
        var secondRetest = _strategy.FindRetestBar(
            TradingDirection.Bearish,
            openingRange!,
            breakout.Timestamp,
            firstRetest!.Timestamp,
            bars
        );

        Assert.NotNull(firstRetest);
        Assert.NotNull(secondRetest);
        Assert.True(secondRetest!.Timestamp > firstRetest.Timestamp);
    }

    [Fact]
    public void BuildTradePlan_Bullish_UsesAtLeastTwoRTarget()
    {
        var retestBar = new TradingBarSnapshot(
            "SPY",
            new DateTimeOffset(2026, 04, 21, 13, 43, 0, TimeSpan.Zero),
            101.8m,
            102.4m,
            101.5m,
            102.1m,
            1000
        );

        var plan = _strategy.BuildTradePlan(
            TradingDirection.Bullish,
            entryPrice: 102m,
            retestBar,
            stopLossBufferPercent: 0.1m,
            rewardToRiskRatio: 2m
        );

        Assert.NotNull(plan);
        Assert.Equal(102m, plan!.EntryPrice);
        Assert.True(plan.TakeProfitPrice - plan.EntryPrice >= (2m * plan.RiskPerUnit));
    }

    [Fact]
    public void BuildTradePlan_Bearish_UsesAtLeastTwoRTarget()
    {
        var retestBar = new TradingBarSnapshot(
            "TSLA",
            new DateTimeOffset(2026, 04, 21, 13, 43, 0, TimeSpan.Zero),
            250m,
            252m,
            248m,
            249m,
            1000
        );

        var plan = _strategy.BuildTradePlan(
            TradingDirection.Bearish,
            entryPrice: 248.5m,
            retestBar,
            stopLossBufferPercent: 0.1m,
            rewardToRiskRatio: 2m
        );

        Assert.NotNull(plan);
        Assert.Equal(248.5m, plan!.EntryPrice);
        Assert.True(plan.EntryPrice - plan.TakeProfitPrice >= (2m * plan.RiskPerUnit));
    }

    private static IReadOnlyCollection<TradingBarSnapshot> BuildBars(
        string symbol,
        DateTimeOffset start,
        IReadOnlyCollection<(decimal Open, decimal High, decimal Low, decimal Close)> candles
    )
    {
        var candlesWithVolume = candles
            .Select((candle, index) =>
                (candle.Open, candle.High, candle.Low, candle.Close, Volume: 1000m + index)
            )
            .ToArray();

        return BuildBars(symbol, start, candlesWithVolume);
    }

    private static IReadOnlyCollection<TradingBarSnapshot> BuildBars(
        string symbol,
        DateTimeOffset start,
        IReadOnlyCollection<(
            decimal Open,
            decimal High,
            decimal Low,
            decimal Close,
            decimal Volume
        )> candles
    )
    {
        var bars = new List<TradingBarSnapshot>(candles.Count);
        var index = 0;
        foreach (var candle in candles)
        {
            bars.Add(
                new TradingBarSnapshot(
                    symbol,
                    start.AddMinutes(index),
                    candle.Open,
                    candle.High,
                    candle.Low,
                    candle.Close,
                    candle.Volume
                )
            );
            index++;
        }

        return bars;
    }
}
