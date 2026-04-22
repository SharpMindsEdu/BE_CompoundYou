using Domain.Services.Trading;

namespace Application.Features.Trading.Automation;

public sealed record TradingOpportunity(string Symbol, TradingDirection Direction, int Score);

public sealed record RetestVerificationRequest(
    string Symbol,
    TradingDirection Direction,
    decimal RangeUpper,
    decimal RangeLower,
    TradingBarSnapshot BreakoutBar,
    TradingBarSnapshot RetestBar,
    IReadOnlyCollection<TradingBarSnapshot> RecentBars
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
