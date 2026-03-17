using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class BilgewaterBullyEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "bilgewater-bully";
    public override string TemplateId => "named.bilgewater-bully";

    public override bool HasKeyword(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        string keyword
    )
    {
        if (!string.Equals(keyword, "Ganking", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return card.TemporaryMightModifier > 0 || card.PermanentMightModifier > 0;
    }
}

