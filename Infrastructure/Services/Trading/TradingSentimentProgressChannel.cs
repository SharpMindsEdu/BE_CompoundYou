using System.Threading.Channels;
using Application.Features.Trading.Live;

namespace Infrastructure.Services.Trading;

public sealed class TradingSentimentProgressChannel : ITradingSentimentProgressChannel
{
    private static readonly TradingSentimentProgress IdleProgress = new(
        DateTimeOffset.MinValue,
        "idle",
        "Keine Sentiment-Analyse aktiv.",
        null,
        null
    );

    private readonly Channel<TradingSentimentProgress> _channel;
    private TradingSentimentProgress _latest = IdleProgress;

    public TradingSentimentProgressChannel()
    {
        _channel = Channel.CreateBounded<TradingSentimentProgress>(
            new BoundedChannelOptions(64)
            {
                SingleReader = false,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.DropOldest,
            }
        );
    }

    public bool TryPublish(TradingSentimentProgress progress)
    {
        ArgumentNullException.ThrowIfNull(progress);

        Volatile.Write(ref _latest, progress);
        return _channel.Writer.TryWrite(progress);
    }

    public TradingSentimentProgress GetLatest()
    {
        return Volatile.Read(ref _latest);
    }

    public IAsyncEnumerable<TradingSentimentProgress> ReadAllAsync(
        CancellationToken cancellationToken = default
    )
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }
}
