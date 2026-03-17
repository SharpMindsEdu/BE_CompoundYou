using Domain.Entities.Riftbound;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class BuffTokenEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "buff";
    public override string TemplateId => "named.buff";

    protected override IReadOnlyDictionary<string, string> BuildData(
        RiftboundCard card,
        string normalizedEffectText
    )
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }
}
