using Domain.Entities.Riftbound;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class JaxUnmatchedEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "jax-unmatched";
    public override string TemplateId => "named.jax-unmatched";

    protected override IReadOnlyDictionary<string, string> BuildData(
        RiftboundCard card,
        string normalizedEffectText
    )
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["grantsEquipmentQuickDrawEverywhere"] = "true",
        };
    }
}
