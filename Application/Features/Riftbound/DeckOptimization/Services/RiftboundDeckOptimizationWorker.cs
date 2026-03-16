using Domain.Entities.Riftbound;
using Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Application.Features.Riftbound.DeckOptimization.Services;

public sealed class RiftboundDeckOptimizationWorker(
    IRiftboundDeckOptimizationRunQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<RiftboundDeckOptimizationWorker> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var runId in queue.DequeueAllAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var executor = scope.ServiceProvider.GetRequiredService<IRiftboundDeckOptimizationRunExecutor>();
                await executor.ExecuteRunAsync(runId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Deck optimization run {RunId} failed unexpectedly in worker loop.",
                    runId
                );

                try
                {
                    using var failureScope = scopeFactory.CreateScope();
                    var runRepository = failureScope.ServiceProvider.GetRequiredService<
                        IRepository<RiftboundDeckOptimizationRun>
                    >();
                    var run = await runRepository.GetById(runId);
                    if (
                        run is not null
                        && !string.Equals(run.Status, "completed", StringComparison.OrdinalIgnoreCase)
                    )
                    {
                        run.Status = "failed";
                        run.ProgressPercent = 100m;
                        run.CompletedOn = DateTimeOffset.UtcNow;
                        run.ErrorMessage = ex.Message;
                        runRepository.Update(run);
                        await runRepository.SaveChanges(CancellationToken.None);
                    }
                }
                catch (Exception persistenceEx)
                {
                    logger.LogError(
                        persistenceEx,
                        "Unable to persist failure status for optimization run {RunId}.",
                        runId
                    );
                }
            }
        }
    }
}
