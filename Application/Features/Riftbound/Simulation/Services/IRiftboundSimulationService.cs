using Application.Features.Riftbound.Simulation.DTOs;
using Application.Shared;

namespace Application.Features.Riftbound.Simulation.Services;

public sealed record RiftboundSimulationCreateRequest(
    long ChallengerDeckId,
    long OpponentDeckId,
    long? Seed,
    string? ChallengerPolicy,
    string? OpponentPolicy
);

public sealed record RiftboundSimulationAutoplayRequest(int MaxSteps);

public sealed record RiftboundDeckTestsRequest(
    long ChallengerDeckId,
    long OpponentDeckId,
    IReadOnlyCollection<long>? Seeds,
    int? RunCount,
    string? ChallengerPolicy,
    string? OpponentPolicy,
    int MaxAutoplaySteps
);

public interface IRiftboundSimulationService
{
    Task<Result<RiftboundDeckSimulationSupportDto>> GetDeckSimulationSupportAsync(
        long userId,
        long deckId,
        CancellationToken cancellationToken
    );

    Task<Result<RiftboundSimulationDto>> CreateSimulationAsync(
        long userId,
        RiftboundSimulationCreateRequest request,
        CancellationToken cancellationToken
    );

    Task<Result<RiftboundSimulationDto>> GetSimulationAsync(
        long userId,
        long simulationId,
        CancellationToken cancellationToken
    );

    Task<Result<RiftboundSimulationDto>> ApplyActionAsync(
        long userId,
        long simulationId,
        string actionId,
        CancellationToken cancellationToken
    );

    Task<Result<RiftboundSimulationDto>> AutoPlayAsync(
        long userId,
        long simulationId,
        RiftboundSimulationAutoplayRequest request,
        CancellationToken cancellationToken
    );

    Task<Result<RiftboundDeckTestsResultDto>> RunDeckTestsAsync(
        long userId,
        RiftboundDeckTestsRequest request,
        CancellationToken cancellationToken
    );
}
