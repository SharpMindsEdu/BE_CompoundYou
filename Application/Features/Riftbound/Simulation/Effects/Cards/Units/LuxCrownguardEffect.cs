using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class LuxCrownguardEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "lux-crownguard";
    public override string TemplateId => "named.lux-crownguard";
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

        card.IsExhausted = true;
        player.RunePool.Energy += 2;
        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "Activate",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["addEnergy"] = "2",
                ["spellsOnly"] = "true",
            }
        );
        return true;
    }
}
