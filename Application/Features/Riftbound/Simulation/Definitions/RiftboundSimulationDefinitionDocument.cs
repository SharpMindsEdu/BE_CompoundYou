namespace Application.Features.Riftbound.Simulation.Definitions;

public sealed record RiftboundSimulationDefinitionDocument(
    string RulesetVersion,
    IReadOnlyCollection<string> SupportedKeywords,
    IReadOnlyCollection<RiftboundSimulationCardDefinition> Cards
);

public sealed record RiftboundSimulationCardDefinition(
    string? ReferenceId,
    string? Name,
    string? Type,
    IReadOnlyCollection<string>? Keywords,
    IReadOnlyCollection<string>? EffectSteps
);
