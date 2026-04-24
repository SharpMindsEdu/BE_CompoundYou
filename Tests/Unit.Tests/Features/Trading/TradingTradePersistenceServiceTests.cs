using System.Text.Json;
using Application.Features.Trading.Automation;
using Domain.Entities;
using Domain.Services.Trading;
using Infrastructure;
using Infrastructure.Services.Trading;
using Microsoft.EntityFrameworkCore;

namespace Unit.Tests.Features.Trading;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.TradingTests)]
public sealed class TradingTradePersistenceServiceTests
{
    [Fact]
    public async Task RecordSubmittedAsync_PreservesBearishDirection_ForLongPutEntry()
    {
        await using var db = CreateDbContext();
        var service = new TradingTradePersistenceService(db);
        var submittedAt = new DateTimeOffset(2026, 4, 22, 14, 30, 0, TimeSpan.Zero);

        await service.RecordSubmittedAsync(
            new TradingOrderSubmissionResult(
                "alpaca-order-1",
                "NVDA260619P00100000",
                "new",
                "buy",
                1m
            ),
            new TradingTradeSubmissionSnapshot(
                "NVDA",
                TradingDirection.Bearish,
                1m,
                2.50m,
                120m,
                110m,
                0m,
                90,
                85,
                submittedAt,
                null,
                submittedAt
            ),
            new TradingOrderSnapshot(
                "alpaca-order-1",
                "NVDA260619P00100000",
                "new",
                "buy",
                "market",
                1m,
                0m,
                0m,
                submittedAt,
                null,
                null,
                submittedAt,
                []
            )
        );

        var trade = await db.TradingTrades.SingleAsync();
        Assert.Equal(TradingDirection.Bearish, trade.Direction);
        Assert.Equal("NVDA", trade.Symbol);
    }

    [Theory]
    [InlineData("TSLA260501P00370000", "TSLA")]
    [InlineData("NVDA260619P00100000", "NVDA")]
    [InlineData("AAPL240419C00150000", "AAPL")]
    [InlineData("SPY260101C00400000", "SPY")]
    [InlineData("TSLA", "TSLA")]
    [InlineData("AAPL", "AAPL")]
    public async Task RecordSubmittedAsync_StoresUnderlyingSymbol_ForOptionAndEquity(
        string contractSymbol,
        string expectedSymbol
    )
    {
        await using var db = CreateDbContext();
        var service = new TradingTradePersistenceService(db);
        var submittedAt = new DateTimeOffset(2026, 4, 22, 14, 30, 0, TimeSpan.Zero);

        await service.RecordSubmittedAsync(
            new TradingOrderSubmissionResult("order-sym-test", contractSymbol, "new", "buy", 1m),
            new TradingTradeSubmissionSnapshot(
                expectedSymbol,
                TradingDirection.Bullish,
                1m,
                1m,
                0m,
                0m,
                0m,
                0,
                0,
                submittedAt,
                null,
                submittedAt
            ),
            new TradingOrderSnapshot(
                "order-sym-test",
                contractSymbol,
                "new",
                "buy",
                "market",
                1m,
                0m,
                0m,
                submittedAt,
                null,
                null,
                submittedAt,
                []
            )
        );

        var trade = await db.TradingTrades.SingleAsync();
        Assert.Equal(expectedSymbol, trade.Symbol);
    }

    [Fact]
    public async Task RecordExitFillAsync_ComputesOptionPnlWithContractMultiplier()
    {
        await using var db = CreateDbContext();
        var service = new TradingTradePersistenceService(db);
        var submittedAt = new DateTimeOffset(2026, 4, 22, 14, 30, 0, TimeSpan.Zero);
        var entryFilledAt = submittedAt.AddMinutes(1);
        var exitFilledAt = submittedAt.AddMinutes(8);

        var parentOrderSnapshot = new TradingOrderSnapshot(
            "alpaca-order-2",
            "TSLA260619P00180000",
            "filled",
            "buy",
            "market",
            2m,
            2m,
            4.00m,
            submittedAt,
            entryFilledAt,
            null,
            exitFilledAt,
            []
        );

        await service.RecordSubmittedAsync(
            new TradingOrderSubmissionResult(
                "alpaca-order-2",
                "TSLA260619P00180000",
                "filled",
                "buy",
                2m
            ),
            new TradingTradeSubmissionSnapshot(
                "TSLA",
                TradingDirection.Bearish,
                2m,
                4.00m,
                190m,
                170m,
                0m,
                80,
                81,
                submittedAt,
                null,
                submittedAt
            ),
            parentOrderSnapshot
        );

        await service.RecordExitFillAsync(
            "alpaca-order-2",
            parentOrderSnapshot,
            new TradingOrderSnapshot(
                "alpaca-order-2-exit",
                "TSLA260619P00180000",
                "filled",
                "sell",
                "market",
                2m,
                2m,
                2.00m,
                exitFilledAt,
                exitFilledAt,
                null,
                exitFilledAt,
                []
            ),
            "TakeProfit"
        );

        var trade = await db.TradingTrades.SingleAsync();
        Assert.Equal(TradingTradeStatus.Closed, trade.Status);
        Assert.Equal(TradingDirection.Bearish, trade.Direction);
        Assert.Equal(-400m, trade.RealizedProfitLoss);
    }

    [Fact]
    public async Task RecordExitFillAsync_ComputesLongPutLoss_WhenExitPriceIsLowerThanEntry()
    {
        await using var db = CreateDbContext();
        var service = new TradingTradePersistenceService(db);
        var submittedAt = new DateTimeOffset(2026, 4, 24, 18, 25, 50, TimeSpan.Zero);
        var entryFilledAt = submittedAt;
        var exitFilledAt = submittedAt.AddSeconds(23);

        var parentOrderSnapshot = new TradingOrderSnapshot(
            "alpaca-order-pltr-put",
            "PLTR260501P00141000",
            "filled",
            "buy",
            "market",
            10m,
            10m,
            4.15m,
            submittedAt,
            entryFilledAt,
            null,
            exitFilledAt,
            []
        );

        await service.RecordSubmittedAsync(
            new TradingOrderSubmissionResult(
                "alpaca-order-pltr-put",
                "PLTR260501P00141000",
                "filled",
                "buy",
                10m
            ),
            new TradingTradeSubmissionSnapshot(
                "PLTR",
                TradingDirection.Bearish,
                10m,
                140.82m,
                141.10m,
                140.26m,
                0.28m,
                82,
                75,
                submittedAt,
                null,
                submittedAt
            ),
            parentOrderSnapshot
        );

        await service.RecordExitFillAsync(
            "alpaca-order-pltr-put",
            parentOrderSnapshot,
            new TradingOrderSnapshot(
                "alpaca-order-pltr-put-exit",
                "PLTR260501P00141000",
                "filled",
                "sell",
                "market",
                10m,
                10m,
                4.05m,
                exitFilledAt,
                exitFilledAt,
                null,
                exitFilledAt,
                []
            ),
            "StopLoss"
        );

        var trade = await db.TradingTrades.SingleAsync();
        Assert.Equal(TradingTradeStatus.Closed, trade.Status);
        Assert.Equal(TradingDirection.Bearish, trade.Direction);
        Assert.Equal(-100m, trade.RealizedProfitLoss);
    }

    [Fact]
    public async Task RecordSubmittedAsync_StoresSignalInsightsAuditPayload()
    {
        await using var db = CreateDbContext();
        var service = new TradingTradePersistenceService(db);
        var submittedAt = new DateTimeOffset(2026, 4, 22, 14, 30, 0, TimeSpan.Zero);

        await service.RecordSubmittedAsync(
            new TradingOrderSubmissionResult("alpaca-order-insights", "AAPL", "new", "buy", 1m),
            new TradingTradeSubmissionSnapshot(
                "AAPL",
                TradingDirection.Bullish,
                1m,
                195m,
                190m,
                205m,
                5m,
                92,
                86,
                submittedAt,
                new TradingSignalInsights(
                    OptionStrategyBias: "LongCall",
                    SentimentScore: 0.42,
                    SentimentLabel: "Bullish",
                    SentimentRelevance: 0.97,
                    SentimentSummary: "Fresh directly relevant news is positive.",
                    CandleBias: "Bullish",
                    CandleSummary: "Prior candle closed near the high.",
                    Reason: "Sentiment and price action are aligned.",
                    RiskNotes: "Invalid below prior support."
                ),
                submittedAt
            ),
            null
        );

        var trade = await db.TradingTrades.SingleAsync();
        Assert.NotNull(trade.SignalInsightsJson);

        var insights = JsonSerializer.Deserialize<TradingSignalInsights>(trade.SignalInsightsJson!);
        Assert.NotNull(insights);
        Assert.Equal("LongCall", insights!.OptionStrategyBias);
        Assert.Equal("Prior candle closed near the high.", insights.CandleSummary);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"trading-trade-tests-{Guid.NewGuid()}")
            .Options;
        return new TradingPersistenceTestDbContext(options);
    }

    private sealed class TradingPersistenceTestDbContext : ApplicationDbContext
    {
        public TradingPersistenceTestDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options, "public") { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TradingTrade>();
        }
    }
}
