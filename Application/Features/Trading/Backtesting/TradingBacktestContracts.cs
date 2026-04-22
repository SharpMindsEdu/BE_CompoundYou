using Domain.Services.Trading;

namespace Application.Features.Trading.Backtesting;

public sealed record TradingBacktestRequest(
    DateOnly StartDate,
    DateOnly EndDate,
    string? WatchlistId = null,
    int? MaxOpportunities = null,
    int? MinimumSentimentScore = null,
    int? MinimumRetestScore = null,
    bool UseAiSentiment = true,
    bool UseAiRetestValidation = true
);

public sealed record TradingBacktestDayResult(
    DateOnly Date,
    int OpportunitiesConsidered,
    int TradesExecuted,
    decimal DayPnl
);

public sealed record TradingBacktestTradeResult(
    DateOnly Date,
    string Symbol,
    TradingDirection Direction,
    int SentimentScore,
    int RetestScore,
    decimal EntryPrice,
    decimal StopLossPrice,
    decimal TakeProfitPrice,
    decimal ExitPrice,
    decimal ProfitLoss,
    decimal RMultiple,
    string ExitReason
);

public sealed record TradingBacktestResult(
    DateOnly StartDate,
    DateOnly EndDate,
    int CalendarDays,
    int TradingDaysEvaluated,
    int TotalTrades,
    int Wins,
    int Losses,
    int FlatExits,
    decimal TotalPnl,
    decimal AveragePnlPerTrade,
    decimal WinRatePercent,
    IReadOnlyCollection<TradingBacktestDayResult> Days,
    IReadOnlyCollection<TradingBacktestTradeResult> Trades
);

public interface ITradingBacktestService
{
    Task<TradingBacktestResult> RunAsync(
        TradingBacktestRequest request,
        CancellationToken cancellationToken = default
    );
}
