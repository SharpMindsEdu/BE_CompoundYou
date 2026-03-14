namespace Application.Features.Riftbound.Simulation.Policies;

public sealed class MovePolicyResolver(
    IEnumerable<IMovePolicy> policies,
    HeuristicMovePolicy heuristic
) : IMovePolicyResolver
{
    private readonly Dictionary<string, IMovePolicy> _policies = policies.ToDictionary(
        x => x.PolicyId,
        x => x,
        StringComparer.OrdinalIgnoreCase
    );

    public IMovePolicy Resolve(string? policyId)
    {
        if (!string.IsNullOrWhiteSpace(policyId) && _policies.TryGetValue(policyId, out var policy))
        {
            return policy;
        }

        return heuristic;
    }
}
