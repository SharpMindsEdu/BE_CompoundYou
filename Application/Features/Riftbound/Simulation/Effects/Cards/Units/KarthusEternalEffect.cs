using Domain.Entities.Riftbound;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class KarthusEternalEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "karthus-eternal";
    public override string TemplateId => "named.karthus-eternal";

    protected override IReadOnlyDictionary<string, string> BuildData(
        RiftboundCard card,
        string normalizedEffectText
    )
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["friendlyDeathknellAdditionalTriggers"] = "1",
        };
    }
}
