using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class SealOfDiscordEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "seal-of-discord";
    public override string TemplateId => "named.seal-of-discord";
    public override bool HasActivatedAbility => true;

    protected override IReadOnlyDictionary<string, string> BuildData(
        RiftboundCard card,
        string normalizedEffectText
    )
    {
        var domain = RiftboundEffectTextParser.ResolvePrimaryDomain(card, normalizedEffectText);
        if (string.IsNullOrWhiteSpace(domain))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["addPowerDomain"] = domain,
            ["addPowerAmount"] = "1",
        };
    }

    public override bool TryActivateAbility(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card
    )
    {
        if (card.IsExhausted)
        {
            return false;
        }

        var domain = runtime.ReadEffectDataString(card, "addPowerDomain");
        var amount = runtime.ReadIntEffectData(card, "addPowerAmount", fallback: 1);
        if (string.IsNullOrWhiteSpace(domain))
        {
            return false;
        }

        card.IsExhausted = true;
        runtime.AddPower(player, domain, amount);
        return true;
    }
}
