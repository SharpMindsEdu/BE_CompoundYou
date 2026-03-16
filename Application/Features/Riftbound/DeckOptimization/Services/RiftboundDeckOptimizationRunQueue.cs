using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace Application.Features.Riftbound.DeckOptimization.Services;

public sealed class RiftboundDeckOptimizationRunQueue : IRiftboundDeckOptimizationRunQueue
{
    private readonly Channel<long> _channel = Channel.CreateUnbounded<long>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false,
        }
    );

    public ValueTask QueueAsync(long runId, CancellationToken cancellationToken)
    {
        return _channel.Writer.WriteAsync(runId, cancellationToken);
    }

    public async IAsyncEnumerable<long> DequeueAllAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        while (await _channel.Reader.WaitToReadAsync(cancellationToken))
        {
            while (_channel.Reader.TryRead(out var runId))
            {
                yield return runId;
            }
        }
    }
}
