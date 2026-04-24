using Domain.Services.Trading;

namespace Application.Features.Trading.Live;

public sealed record TradingTickerUpdate(
    string Symbol,
    string Interval,
    bool IsClosed,
    TradingBarSnapshot Bar
);

public interface ITradingTickerUpdateChannel
{
    bool TryPublish(TradingBarSnapshot bar);

    IAsyncEnumerable<TradingBarSnapshot> ReadAllAsync(
        CancellationToken cancellationToken = default
    );
}
