using Application.Features.Riftbound.Simulation.Definitions;
using Domain.Entities.Riftbound;

namespace Application.Features.Riftbound.Simulation.Services;

public interface IRiftboundSimulationDefinitionRegistry
{
    string RulesetVersion { get; }
    IReadOnlyCollection<string> SupportedKeywords { get; }
    IReadOnlyCollection<RiftboundRuleCorrection> RuleCorrections { get; }
    RiftboundSimulationCardDefinition? FindDefinition(RiftboundCard card);
    bool IsCardSupported(RiftboundCard card);
}
