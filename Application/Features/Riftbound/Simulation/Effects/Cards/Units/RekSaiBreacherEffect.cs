using Domain.Entities.Riftbound;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class RekSaiBreacherEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "rek-sai-breacher";
    public override string TemplateId => "named.rek-sai-breacher";

    protected override IReadOnlyDictionary<string, string> BuildData(
        RiftboundCard card,
        string normalizedEffectText
    )
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["grantAccelerateForNonHandPlay"] = "true",
        };
    }
}
