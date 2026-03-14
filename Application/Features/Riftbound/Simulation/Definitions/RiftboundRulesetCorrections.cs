namespace Application.Features.Riftbound.Simulation.Definitions;

public sealed record RiftboundRuleCorrection(string Key, string Description);

public static class RiftboundRulesetCorrections
{
    public static readonly IReadOnlyCollection<RiftboundRuleCorrection> V1Corrections =
    [
        new("turned-to-exhausted", "All references to turned state are normalized to exhausted."),
        new("showdown-combat-cleanup-staging", "Showdown and combat staging is finalized during cleanup."),
        new("hidden-facedown-control-loss", "Hidden and facedown attachments clean up when controller is lost."),
        new("pending-finalized-chain", "Chain items are modeled as pending first and then finalized."),
        new("action-reaction-timing", "Action and reaction timing windows are validated by state transitions."),
        new("reveal-attachment-clarification", "Reveal and attachment timing uses explicit effect steps."),
        new("supported-keyword-semantics", "Supported keywords use versioned simulation semantics."),
    ];
}
