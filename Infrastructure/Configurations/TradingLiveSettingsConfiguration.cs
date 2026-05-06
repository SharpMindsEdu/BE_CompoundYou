using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations;

public class TradingLiveSettingsConfiguration : IEntityTypeConfiguration<TradingLiveSettings>
{
    public void Configure(EntityTypeBuilder<TradingLiveSettings> builder)
    {
        builder.ToTable("trading_live_settings");
        builder.Property(x => x.MinimumEntryDistanceFromRangeFraction).HasPrecision(18, 6);
        builder.Property(x => x.StopLossBufferFraction).HasPrecision(18, 6);
        builder.Property(x => x.RewardToRiskRatio).HasPrecision(18, 6);
        builder.Property(x => x.RiskPerTradeFraction).HasPrecision(18, 6);
        builder.Property(x => x.BreakEvenAtRMultiple).HasPrecision(18, 6);
        builder.Property(x => x.MaxDailyLossFraction).HasPrecision(18, 6);
        builder.Property(x => x.PartialTakeProfitFraction).HasPrecision(18, 6);
        builder.Property(x => x.TrailingStopRiskMultiple).HasPrecision(18, 6);
        builder.Property(x => x.DirectionalIndicatorModesJson).HasColumnType("text");
    }
}
