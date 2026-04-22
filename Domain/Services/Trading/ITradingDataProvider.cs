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
}
