using Domain.Entities.Riftbound;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class JaullFishEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "jaull-fish";
    public override string TemplateId => "named.jaull-fish";

    protected override IReadOnlyDictionary<string, string> BuildData(
        RiftboundCard card,
        string normalizedEffectText
    )
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["energyDiscountPerFriendlyMightyUnits"] = "2",
        };
    }
}
