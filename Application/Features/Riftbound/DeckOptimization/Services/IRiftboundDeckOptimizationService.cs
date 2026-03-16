using Application.Features.Riftbound.DeckOptimization.DTOs;
using Application.Shared;

namespace Application.Features.Riftbound.DeckOptimization.Services;

public sealed record RiftboundDeckOptimizationRunRequest(
    int? PopulationSize,
    int? Generations,
    int? SeedsPerMatch,
    int? MaxAutoplaySteps,
    long? Seed
);

public interface IRiftboundDeckOptimizationService
{
    Task<Result<RiftboundDeckOptimizationRunDto>> CreateRunAsync(
        long userId,
        RiftboundDeckOptimizationRunRequest request,
        CancellationToken cancellationToken
    );

    Task<Result<RiftboundDeckOptimizationRunDto>> GetRunAsync(
        long userId,
        long runId,
        CancellationToken cancellationToken
    );

    Task<Result<RiftboundDeckOptimizationLeaderboardDto>> GetLeaderboardAsync(
        long userId,
        long runId,
        CancellationToken cancellationToken
    );
}

public interface IRiftboundDeckOptimizationRunExecutor
{
    Task ExecuteRunAsync(long runId, CancellationToken cancellationToken);
}
