using Domain.Entities.Riftbound;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class NocturneHorrifyingEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "nocturne-horrifying";
    public override string TemplateId => "named.nocturne-horrifying";

    protected override IReadOnlyDictionary<string, string> BuildData(
        RiftboundCard card,
        string normalizedEffectText
    )
    {
        var domain = card.Color?.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
        if (string.IsNullOrWhiteSpace(domain))
        {
            domain = "Chaos";
        }

        var normalizedDomain = RiftboundEffectTextParser.NormalizeDomain(domain);
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["topDeckRevealPlay.enabled"] = "true",
            ["topDeckRevealPlay.energyCost"] = "0",
            [$"topDeckRevealPlay.powerCost.{normalizedDomain}"] = "1",
        };
    }
}
