using Domain.Services.Trading;

namespace Application.Features.Trading.Automation;

public sealed record TradingSignalInsights(
    string? OptionStrategyBias = null,
    double? SentimentScore = null,
    string? SentimentLabel = null,
    double? SentimentRelevance = null,
    string? SentimentSummary = null,
    string? CandleBias = null,
    string? CandleSummary = null,
    string? Reason = null,
    string? RiskNotes = null
);

public sealed record TradingOpportunity(
    string Symbol,
    TradingDirection Direction,
    int Score,
    TradingSignalInsights? SignalInsights = null
);

public sealed record RetestVerificationRequest(
    string Symbol,
    TradingDirection Direction,
    decimal RangeUpper,
    decimal RangeLower,
    TradingBarSnapshot BreakoutBar,
    TradingBarSnapshot RetestBar,
    IReadOnlyCollection<TradingBarSnapshot> RecentBars,
    DateTimeOffset? EvaluationCutoffTimestampUtc = null
);

public sealed record RetestVerificationResult(
    string Symbol,
    TradingDirection Direction,
    int Score
);

public interface ITradingSignalAgent
{
    Task<IReadOnlyCollection<TradingOpportunity>> AnalyzeWatchlistSentimentAsync(
        IReadOnlyCollection<string> symbols,
        int maxOpportunities,
        DateOnly? tradingDate = null,
        CancellationToken cancellationToken = default
    );

    Task<RetestVerificationResult?> VerifyRetestAsync(
        RetestVerificationRequest request,
        DateOnly? tradingDate = null,
        CancellationToken cancellationToken = default
    );
}
