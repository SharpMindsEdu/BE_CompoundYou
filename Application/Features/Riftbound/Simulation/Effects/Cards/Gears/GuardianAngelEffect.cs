using Domain.Entities.Riftbound;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class GuardianAngelEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "guardian-angel";
    public override string TemplateId => "named.guardian-angel";

    protected override IReadOnlyDictionary<string, string> BuildData(
        RiftboundCard card,
        string normalizedEffectText
    )
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["attachedMightBonus"] = "1",
        };
    }
}

