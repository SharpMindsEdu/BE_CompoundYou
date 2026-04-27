using Domain.Entities;

namespace Infrastructure.Services.Trading;

public sealed class TradingSentimentAnalysisRecord : TrackedEntity
{
    public long Id { get; set; }

    public DateTimeOffset AnalyzedAtUtc { get; set; }

    public DateOnly TradingDate { get; set; }

    public string? AgentText { get; set; }

    public string AllOpportunitiesJson { get; set; } = "[]";
}
