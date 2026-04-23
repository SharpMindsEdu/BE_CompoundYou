using Application.Features.Trading.Queries;
using Application.Shared;
using Domain.Entities;
using Domain.Services.Trading;
using Unit.Tests.Features.Base;

namespace Unit.Tests.Features.Trading.QueryHandlerTests;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.TradingTests)]
public sealed class GetTradingTradesQueryHandlerTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper
) : FeatureTestBase(fixture, outputHelper)
{
    [Fact]
    public async Task GetTradingTrades_WithSymbolAndStatus_ReturnsFilteredPage()
    {
        var now = new DateTimeOffset(2026, 4, 23, 12, 0, 0, TimeSpan.Zero);
        PersistWithDatabase(db =>
            db.AddRange(
                BuildTrade(
                    "TSLA",
                    TradingDirection.Bullish,
                    TradingTradeStatus.Closed,
                    "order-tsla-1",
                    now.AddMinutes(-30),
                    150m,
                    1.5m,
                    "TakeProfit"
                ),
                BuildTrade(
                    "AMD",
                    TradingDirection.Bearish,
                    TradingTradeStatus.Closed,
                    "order-amd-1",
                    now.AddMinutes(-20),
                    -80m,
                    -0.8m,
                    "StopLoss"
                ),
                BuildTrade(
                    "TSLA",
                    TradingDirection.Bullish,
                    TradingTradeStatus.EntryFilled,
                    "order-tsla-2",
                    now.AddMinutes(-10)
                )
            )
        );

        var result = await Send(
            new GetTradingTrades.Query(Symbol: "tsla", Status: TradingTradeStatus.Closed, PageSize: 25),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data!.Items);
        Assert.Equal("TSLA", result.Data.Items.First().Symbol);
        Assert.Equal(TradingTradeStatus.Closed, result.Data.Items.First().Status);
    }

    [Fact]
    public async Task GetTradingTradeById_WithUnknownId_ReturnsNotFoundResult()
    {
        var result = await Send(
            new GetTradingTradeById.Query(999_999),
            TestContext.Current.CancellationToken
        );

        Assert.False(result.Succeeded);
        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task GetTradingTradesSummary_ComputesExpectedMetrics()
    {
        var now = new DateTimeOffset(2026, 4, 23, 12, 0, 0, TimeSpan.Zero);
        PersistWithDatabase(db =>
            db.AddRange(
                BuildTrade(
                    "TSLA",
                    TradingDirection.Bullish,
                    TradingTradeStatus.Closed,
                    "order-summary-1",
                    now.AddMinutes(-40),
                    200m,
                    2m,
                    "TakeProfit"
                ),
                BuildTrade(
                    "TSLA",
                    TradingDirection.Bearish,
                    TradingTradeStatus.Closed,
                    "order-summary-2",
                    now.AddMinutes(-30),
                    -100m,
                    -1m,
                    "StopLoss"
                ),
                BuildTrade(
                    "TSLA",
                    TradingDirection.Bullish,
                    TradingTradeStatus.EntryFilled,
                    "order-summary-3",
                    now.AddMinutes(-20)
                )
            )
        );

        var result = await Send(
            new GetTradingTradesSummary.Query(Symbol: "TSLA"),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Data);
        Assert.Equal(3, result.Data!.TotalTrades);
        Assert.Equal(2, result.Data.ClosedTrades);
        Assert.Equal(1, result.Data.EntryFilledTrades);
        Assert.Equal(1, result.Data.WinningTrades);
        Assert.Equal(1, result.Data.LosingTrades);
        Assert.Equal(100m, result.Data.TotalRealizedProfitLoss);
        Assert.Equal(50m, result.Data.AverageRealizedProfitLoss);
        Assert.Equal(0.5m, result.Data.AverageRealizedRMultiple);
        Assert.Equal(50m, result.Data.WinRatePercent);
    }

    private static TradingTrade BuildTrade(
        string symbol,
        TradingDirection direction,
        TradingTradeStatus status,
        string orderId,
        DateTimeOffset submittedAtUtc,
        decimal? realizedProfitLoss = null,
        decimal? realizedRMultiple = null,
        string? exitReason = null
    )
    {
        var now = DateTimeOffset.UtcNow;
        return new TradingTrade
        {
            Symbol = symbol,
            Direction = direction,
            Status = status,
            AlpacaOrderId = orderId,
            Quantity = 1m,
            PlannedEntryPrice = 100m,
            PlannedStopLossPrice = 95m,
            PlannedTakeProfitPrice = 110m,
            PlannedRiskPerUnit = 5m,
            ActualEntryPrice = status == TradingTradeStatus.Submitted ? null : 100m,
            ActualExitPrice = status == TradingTradeStatus.Closed ? 101m : null,
            RealizedProfitLoss = realizedProfitLoss,
            RealizedRMultiple = realizedRMultiple,
            ExitReason = exitReason,
            SubmittedAtUtc = submittedAtUtc,
            EntryFilledAtUtc = status == TradingTradeStatus.Submitted ? null : submittedAtUtc.AddMinutes(1),
            ExitFilledAtUtc = status == TradingTradeStatus.Closed ? submittedAtUtc.AddMinutes(5) : null,
            CreatedOn = now,
            UpdatedOn = now,
            DeletedOn = DateTimeOffset.MinValue,
        };
    }
}
