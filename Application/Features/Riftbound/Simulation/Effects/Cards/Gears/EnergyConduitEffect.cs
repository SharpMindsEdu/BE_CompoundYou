using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class EnergyConduitEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "energy-conduit";
    public override string TemplateId => "named.energy-conduit";
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
        player.RunePool.Energy += 1;
        return true;
    }
}

