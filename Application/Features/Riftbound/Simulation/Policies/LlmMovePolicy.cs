namespace Application.Features.Riftbound.Simulation.Policies;

public sealed class LlmMovePolicy(RiftboundModelMovePolicy modelPolicy) : IMovePolicy
{
    public const string Id = "llm";

    public string PolicyId => Id;

    public Task<string?> ChooseActionIdAsync(
        RiftboundMovePolicyContext context,
        CancellationToken cancellationToken
    )
    {
        return modelPolicy.ChooseActionIdAsync(context, cancellationToken);
    }
}
