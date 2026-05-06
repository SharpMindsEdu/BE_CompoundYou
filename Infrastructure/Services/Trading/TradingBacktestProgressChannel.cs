using System.Threading.Channels;
using Application.Features.Trading.Backtesting;

namespace Infrastructure.Services.Trading;

public sealed class TradingBacktestProgressChannel : ITradingBacktestProgressChannel
{
    private static readonly TradingBacktestProgress IdleProgress = new(
        Guid.Empty,
        DateTimeOffset.MinValue,
        "idle",
        DateOnly.MinValue,
        DateOnly.MinValue,
        0,
        0,
        0m,
        "Kein Backtest aktiv."
    );

    private readonly Channel<TradingBacktestProgress> _channel;
    private TradingBacktestProgress _latest = IdleProgress;

    public TradingBacktestProgressChannel()
    {
        _channel = Channel.CreateBounded<TradingBacktestProgress>(
            new BoundedChannelOptions(256)
            {
                SingleReader = false,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.DropOldest,
            }
        );
    }

    public bool TryPublish(TradingBacktestProgress progress)
    {
        ArgumentNullException.ThrowIfNull(progress);

        Volatile.Write(ref _latest, progress);
        return _channel.Writer.TryWrite(progress);
    }

    public TradingBacktestProgress GetLatest()
    {
        return Volatile.Read(ref _latest);
    }

    public IAsyncEnumerable<TradingBacktestProgress> ReadAllAsync(
        CancellationToken cancellationToken = default
    )
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }
}
