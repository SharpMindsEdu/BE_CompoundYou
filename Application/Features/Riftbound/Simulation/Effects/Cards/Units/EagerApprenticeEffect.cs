using Domain.Entities.Riftbound;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class EagerApprenticeEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "eager-apprentice";
    public override string TemplateId => "named.eager-apprentice";

    protected override IReadOnlyDictionary<string, string> BuildData(
        RiftboundCard card,
        string normalizedEffectText
    )
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["spellEnergyAuraDiscount"] = "1",
            ["spellEnergyAuraMinimum"] = "1",
        };
    }
}

