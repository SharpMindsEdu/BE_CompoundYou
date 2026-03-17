using Application.Features.Riftbound.Simulation.Policies;

namespace Unit.Tests.Features.Riftbound.Simulation;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.RiftboundTests)]
public class MovePolicyResolverTests
{
    [Fact]
    public void Resolve_ReturnsRegisteredPolicy_CaseInsensitive()
    {
        var heuristic = new HeuristicMovePolicy();
        var custom = new StubMovePolicy("MyPolicy");
        var resolver = new MovePolicyResolver([custom, heuristic], heuristic);

        var resolved = resolver.Resolve("mypolicy");

        Assert.Same(custom, resolved);
    }

    [Fact]
    public void Resolve_ReturnsHeuristic_WhenPolicyIsMissingOrNull()
    {
        var heuristic = new HeuristicMovePolicy();
        var resolver = new MovePolicyResolver([heuristic], heuristic);

        var fromUnknown = resolver.Resolve("does-not-exist");
        var fromNull = resolver.Resolve(null);
        var fromWhitespace = resolver.Resolve("  ");

        Assert.Same(heuristic, fromUnknown);
        Assert.Same(heuristic, fromNull);
        Assert.Same(heuristic, fromWhitespace);
    }

    private sealed class StubMovePolicy(string id) : IMovePolicy
    {
        public string PolicyId => id;

        public Task<string?> ChooseActionIdAsync(
            RiftboundMovePolicyContext context,
            CancellationToken cancellationToken
        )
        {
            return Task.FromResult<string?>(null);
        }
    }
}
