using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations;

public sealed class TradingTradeConfiguration : IEntityTypeConfiguration<TradingTrade>
{
    public void Configure(EntityTypeBuilder<TradingTrade> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Symbol).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Direction).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).IsRequired();

        builder.Property(x => x.AlpacaOrderId).HasMaxLength(128).IsRequired();
        builder.Property(x => x.AlpacaTakeProfitOrderId).HasMaxLength(128);
        builder.Property(x => x.AlpacaStopLossOrderId).HasMaxLength(128);
        builder.Property(x => x.AlpacaExitOrderId).HasMaxLength(128);
        builder.Property(x => x.ExitReason).HasMaxLength(64);
        builder.Property(x => x.AlpacaOrderStatus).HasMaxLength(64);
        builder.Property(x => x.AlpacaExitOrderStatus).HasMaxLength(64);
        builder.Property(x => x.SignalInsightsJson).HasColumnType("text");
        builder.Property(x => x.RetestAttemptsJson).HasColumnType("text");
        builder.Property(x => x.FeeBreakdownJson).HasColumnType("text");

        builder.Property(x => x.Quantity).HasPrecision(18, 6);
        builder.Property(x => x.PlannedEntryPrice).HasPrecision(18, 6);
        builder.Property(x => x.PlannedStopLossPrice).HasPrecision(18, 6);
        builder.Property(x => x.PlannedTakeProfitPrice).HasPrecision(18, 6);
        builder.Property(x => x.PlannedRiskPerUnit).HasPrecision(18, 6);
        builder.Property(x => x.ActualEntryPrice).HasPrecision(18, 6);
        builder.Property(x => x.ActualExitPrice).HasPrecision(18, 6);
        builder.Property(x => x.RealizedProfitLoss).HasPrecision(18, 6);
        builder.Property(x => x.RealizedGrossProfitLoss).HasPrecision(18, 6);
        builder.Property(x => x.RealizedTotalFees).HasPrecision(18, 6);
        builder.Property(x => x.RealizedAlpacaFees).HasPrecision(18, 6);
        builder.Property(x => x.RealizedSpreadCost).HasPrecision(18, 6);
        builder.Property(x => x.RealizedRMultiple).HasPrecision(18, 6);
        builder.Property(x => x.OpeningRangeHigh).HasPrecision(18, 6);
        builder.Property(x => x.OpeningRangeLow).HasPrecision(18, 6);
        builder.Property(x => x.OptionPlannedEntryPrice).HasPrecision(18, 6);
        builder.Property(x => x.OptionPlannedStopLossPrice).HasPrecision(18, 6);
        builder.Property(x => x.OptionPlannedTakeProfitPrice).HasPrecision(18, 6);
        builder.Property(x => x.OptionPlannedRiskPerUnit).HasPrecision(18, 6);

        builder.HasIndex(x => x.AlpacaOrderId).IsUnique();
        builder.HasIndex(x => new { x.Symbol, x.SubmittedAtUtc });
        builder.HasIndex(x => x.SentimentAnalysisId);
    }
}
