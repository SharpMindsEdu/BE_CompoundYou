using Infrastructure.Services.Trading;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations;

public sealed class TradingCalendarDayConfiguration : IEntityTypeConfiguration<TradingCalendarDay>
{
    public void Configure(EntityTypeBuilder<TradingCalendarDay> builder)
    {
        builder.HasKey(x => x.Id);

        builder.HasIndex(x => x.Date).IsUnique();
    }
}
