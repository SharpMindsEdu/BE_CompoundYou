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
    bool IsValidRetest,
    int Score,
    decimal OpeningRangeHigh,
    decimal OpeningRangeLow,
    bool BreakoutConfirmed,
    string BreakoutQuality,
    string BreakoutSummary,
    bool RetestConfirmed,
    string RetestQuality,
    string RetestSummary,
    bool ConfirmationCandlePresent,
    string ContinuationBias,
    string? InvalidationReason,
    string Reason,
    string RiskNotes
);

public interface ITradingSignalAgent
{
    Task<IReadOnlyCollection<TradingOpportunity>> AnalyzeWatchlistSentimentAsync(
        IReadOnlyCollection<string> symbols,
        int maxOpportunities,
        DateOnly? tradingDate = null,
        CancellationToken cancellationToken = default,
        Action<string>? onStreamingActivityDelta = null
    );

    Task<RetestVerificationResult?> VerifyRetestAsync(
        RetestVerificationRequest request,
        DateOnly? tradingDate = null,
        CancellationToken cancellationToken = default
    );
}
