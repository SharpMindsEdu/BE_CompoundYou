namespace Application.Features.Trading.Backtesting;

public sealed record TradingBacktestProgress(
    Guid RunId,
    DateTimeOffset GeneratedAtUtc,
    string Phase,
    DateOnly StartDate,
    DateOnly EndDate,
    int TotalCalendarDays,
    int ProcessedCalendarDays,
    decimal PercentComplete,
    string Message
)
{
    public DateOnly? CurrentDate { get; init; }

    public int TradingDaysEvaluated { get; init; }

    public int TradesSoFar { get; init; }

    public decimal CumulativePnl { get; init; }

    public TradingBacktestDayResult? LastDayResult { get; init; }

    public IReadOnlyCollection<TradingBacktestTradeResult>? LastDayTrades { get; init; }

    public string? ErrorMessage { get; init; }
}

public interface ITradingBacktestProgressChannel
{
    bool TryPublish(TradingBacktestProgress progress);

    TradingBacktestProgress GetLatest();

    IAsyncEnumerable<TradingBacktestProgress> ReadAllAsync(
        CancellationToken cancellationToken = default
    );
}
