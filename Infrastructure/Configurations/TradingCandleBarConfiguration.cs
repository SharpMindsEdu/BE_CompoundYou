using Infrastructure.Services.Trading;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations;

public sealed class TradingCandleBarConfiguration : IEntityTypeConfiguration<TradingCandleBar>
{
    public void Configure(EntityTypeBuilder<TradingCandleBar> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Symbol).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Open).HasPrecision(18, 6);
        builder.Property(x => x.High).HasPrecision(18, 6);
        builder.Property(x => x.Low).HasPrecision(18, 6);
        builder.Property(x => x.Close).HasPrecision(18, 6);
        builder.Property(x => x.Volume).HasPrecision(18, 6);

        builder.HasIndex(x => new { x.Symbol, x.TimestampUtc }).IsUnique();
    }
}
