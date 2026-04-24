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

    Task<IReadOnlyCollection<TradingBarSnapshot>> GetBarsInRangeAsync(
        string symbol,
        TradingBarInterval interval,
        DateTimeOffset start,
        DateTimeOffset end,
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

    Task<TradingSessionSnapshot?> GetTradingSessionAsync(
        DateOnly tradingDate,
        CancellationToken cancellationToken = default
    );

    Task<TradingMarketClockSnapshot> GetMarketClockAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<TradingOrderSnapshot>> GetOpenOrdersAsync(
        CancellationToken cancellationToken = default
    );

    Task<TradingOrderSubmissionResult> SubmitBracketOrderAsync(
        TradingBracketOrderRequest request,
        CancellationToken cancellationToken = default
    );

    Task<TradingOptionContractSnapshot?> SelectOptionContractAsync(
        string underlyingSymbol,
        TradingDirection direction,
        decimal underlyingPrice,
        DateOnly tradingDate,
        int minDaysToExpiration,
        int maxDaysToExpiration,
        CancellationToken cancellationToken = default
    );

    Task<TradingOptionQuoteSnapshot?> GetOptionQuoteAsync(
        string optionSymbol,
        CancellationToken cancellationToken = default
    );

    Task<TradingOrderSubmissionResult> SubmitOptionOrderAsync(
        TradingOptionOrderRequest request,
        CancellationToken cancellationToken = default
    );

    Task<TradingOrderSnapshot?> GetOrderAsync(
        string orderId,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyCollection<TradingFeeActivitySnapshot>> GetFeeActivitiesAsync(
        int limit = 500,
        CancellationToken cancellationToken = default
    );
}
