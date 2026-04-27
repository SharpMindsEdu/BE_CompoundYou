using Infrastructure.Services.Trading;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations;

public sealed class TradingSentimentAnalysisRecordConfiguration
    : IEntityTypeConfiguration<TradingSentimentAnalysisRecord>
{
    public void Configure(EntityTypeBuilder<TradingSentimentAnalysisRecord> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.AgentText).HasColumnType("text");
        builder.Property(x => x.AllOpportunitiesJson).HasColumnType("text").IsRequired();

        builder.HasIndex(x => x.AnalyzedAtUtc);
        builder.HasIndex(x => x.TradingDate);
    }
}
