namespace Application.Features.Riftbound.DeckOptimization.Services;

public interface IRiftboundDeckOptimizationRunQueue
{
    ValueTask QueueAsync(long runId, CancellationToken cancellationToken);
    IAsyncEnumerable<long> DequeueAllAsync(CancellationToken cancellationToken);
}
