using Domain.Entities.Riftbound;

namespace Application.Features.Riftbound.Simulation.Effects;

/// <summary>
/// Declares explicit support for a named card that currently has no bespoke runtime logic.
/// This keeps effect resolution deterministic and avoids "unsupported" templates.
/// </summary>
public sealed class RiftboundDeclaredCardEffect(
    string nameIdentifier,
    string templateId,
    IReadOnlyDictionary<string, string>? data = null
)
    : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => nameIdentifier;
    public override string TemplateId => templateId;

    protected override IReadOnlyDictionary<string, string> BuildData(
        RiftboundCard card,
        string normalizedEffectText
    )
    {
        return data is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(data, StringComparer.OrdinalIgnoreCase);
    }
}
