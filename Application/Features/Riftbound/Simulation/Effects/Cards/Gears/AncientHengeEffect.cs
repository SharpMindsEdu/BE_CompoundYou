using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class AncientHengeEffect : RiftboundNamedCardEffectBase
{
    private const string GenericRuneDomain = "__unknown__";

    public override string NameIdentifier => "ancient-henge";
    public override string TemplateId => "named.ancient-henge";
    public override bool HasActivatedAbility => true;

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

        var energyToPay = Math.Max(0, player.RunePool.Energy);
        if (energyToPay <= 0)
        {
            return false;
        }

        player.RunePool.Energy -= energyToPay;
        card.IsExhausted = true;
        runtime.AddPower(player, GenericRuneDomain, energyToPay);
        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "Activate",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["paidEnergy"] = energyToPay.ToString(),
                ["addedRune"] = energyToPay.ToString(),
            }
        );

        return true;
    }
}
