using Domain.Entities.Riftbound;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class BandleTreeEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "bandle-tree";
    public override string TemplateId => "named.bandle-tree";

    protected override IReadOnlyDictionary<string, string> BuildData(
        RiftboundCard card,
        string normalizedEffectText
    )
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["additionalHideCapacity"] = "1",
        };
    }
}

