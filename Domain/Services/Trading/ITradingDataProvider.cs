namespace Domain.Services.Trading;

public interface ITradingDataProvider
{
    Task<TradingAccountSnapshot> GetAccountAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<TradingPositionSnapshot>> GetPositionsAsync(
        CancellationToken cancellationToken = default
    );

    Task<TradingQuoteSnapshot> GetQuoteAsync(string symbol, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<TradingBarSnapshot>> GetRecentBarsAsync(
        string symbol,
        int limit = 50,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyCollection<TradingBarSnapshot>> GetBarsAsync(
        string symbol,
        DateTimeOffset start,
        DateTimeOffset? end = null,
        int limit = 500,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyCollection<string>> GetWatchlistSymbolsAsync(
        string watchlistId,
        CancellationToken cancellationToken = default
    );

    Task<DateTimeOffset?> GetWatchlistMarketOpenUtcAsync(
        string watchlistId,
        DateOnly tradingDate,
        CancellationToken cancellationToken = default
    );

    Task<TradingMarketClockSnapshot> GetMarketClockAsync(CancellationToken cancellationToken = default);

    Task<TradingOrderSubmissionResult> SubmitBracketOrderAsync(
        TradingBracketOrderRequest request,
        CancellationToken cancellationToken = default
    );

    Task<TradingOrderSnapshot?> GetOrderAsync(
        string orderId,
        CancellationToken cancellationToken = default
    );
}
