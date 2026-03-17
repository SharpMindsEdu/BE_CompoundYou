using Domain.Entities.Riftbound;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class EkkoRecurrentEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "ekko-recurrent";
    public override string TemplateId => "named.ekko-recurrent";

    protected override IReadOnlyDictionary<string, string> BuildData(
        RiftboundCard card,
        string normalizedEffectText
    )
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["onDeath.readyAllRunes"] = "true",
            ["onDeath.recycleSelf"] = "true",
        };
    }
}

