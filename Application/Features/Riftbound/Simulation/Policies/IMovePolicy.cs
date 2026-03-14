using Application.Features.Riftbound.Simulation.Engine;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Policies;

public sealed record RiftboundMovePolicyContext(
    GameSession Session,
    int PlayerIndex,
    IReadOnlyCollection<RiftboundLegalAction> LegalActions
);

public interface IMovePolicy
{
    string PolicyId { get; }

    Task<string?> ChooseActionIdAsync(
        RiftboundMovePolicyContext context,
        CancellationToken cancellationToken
    );
}

public interface IMovePolicyResolver
{
    IMovePolicy Resolve(string? policyId);
}
