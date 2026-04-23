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
        Assert.Equal("NVDA260619P00100000", trade.Symbol);
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
        Assert.Equal(400m, trade.RealizedProfitLoss);
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
