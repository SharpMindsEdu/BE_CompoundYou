using System.Threading.Channels;
using Application.Features.Trading.Live;

namespace Infrastructure.Services.Trading;

public sealed class TradingLiveTelemetryChannel : ITradingLiveTelemetryChannel
{
    private static readonly TradingLiveSnapshot EmptySnapshot = new(
        DateTimeOffset.MinValue,
        null,
        false,
        null,
        false,
        []
    );

    private readonly Channel<TradingLiveSnapshot> _channel;
    private TradingLiveSnapshot _latest = EmptySnapshot;

    public TradingLiveTelemetryChannel()
    {
        _channel = Channel.CreateBounded<TradingLiveSnapshot>(
            new BoundedChannelOptions(256)
            {
                SingleReader = false,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.DropOldest,
            }
        );
    }

    public bool TryPublish(TradingLiveSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        Volatile.Write(ref _latest, snapshot);
        return _channel.Writer.TryWrite(snapshot);
    }

    public TradingLiveSnapshot GetLatest()
    {
        return Volatile.Read(ref _latest);
    }

    public IAsyncEnumerable<TradingLiveSnapshot> ReadAllAsync(
        CancellationToken cancellationToken = default
    )
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }
}
