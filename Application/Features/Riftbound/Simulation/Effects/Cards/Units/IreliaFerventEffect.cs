using Domain.Entities.Riftbound;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class IreliaFerventEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "irelia-fervent";
    public override string TemplateId => "named.irelia-fervent";

    protected override IReadOnlyDictionary<string, string> BuildData(
        RiftboundCard card,
        string normalizedEffectText
    )
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["onChosenTempMight"] = "1",
            ["onReadyTempMight"] = "1",
        };
    }
}
