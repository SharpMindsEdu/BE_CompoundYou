using System.Threading.Channels;
using Application.Features.Trading.Live;
using Domain.Services.Trading;

namespace Infrastructure.Services.Trading;

public sealed class TradingTickerUpdateChannel : ITradingTickerUpdateChannel
{
    private readonly Channel<TradingBarSnapshot> _channel;

    public TradingTickerUpdateChannel()
    {
        _channel = Channel.CreateBounded<TradingBarSnapshot>(
            new BoundedChannelOptions(4_096)
            {
                SingleReader = false,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.DropOldest,
            }
        );
    }

    public bool TryPublish(TradingBarSnapshot bar)
    {
        return _channel.Writer.TryWrite(bar);
    }

    public IAsyncEnumerable<TradingBarSnapshot> ReadAllAsync(
        CancellationToken cancellationToken = default
    )
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }
}
