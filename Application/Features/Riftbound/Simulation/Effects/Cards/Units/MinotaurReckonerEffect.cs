using Domain.Entities.Riftbound;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class MinotaurReckonerEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "minotaur-reckoner";
    public override string TemplateId => "named.minotaur-reckoner";

    protected override IReadOnlyDictionary<string, string> BuildData(
        RiftboundCard card,
        string normalizedEffectText
    )
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["preventMoveToBase"] = "true",
        };
    }
}
