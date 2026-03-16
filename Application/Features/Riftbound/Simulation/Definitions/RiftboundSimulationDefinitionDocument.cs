namespace Application.Features.Riftbound.Simulation.Definitions;

public sealed record RiftboundSimulationDefinitionDocument(
    string RulesetVersion,
    string CatalogSnapshotDate,
    IReadOnlyCollection<string> SupportedKeywords,
    IReadOnlyCollection<RiftboundSimulationCardDefinition> Cards
);

public sealed record RiftboundSimulationCardDefinition(
    string? ReferenceId,
    string? Name,
    string? Type,
    string? Supertype,
    bool Supported,
    string TemplateId,
    IReadOnlyCollection<string>? Keywords,
    IReadOnlyDictionary<string, string>? OverrideData
);
