using Domain.Entities.Riftbound;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class FerrousForerunnerEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "ferrous-forerunner";
    public override string TemplateId => "named.ferrous-forerunner";

    protected override IReadOnlyDictionary<string, string> BuildData(
        RiftboundCard card,
        string normalizedEffectText
    )
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["onDeath.spawnMechTokens"] = "2",
            ["onDeath.tokenMight"] = "3",
        };
    }
}
