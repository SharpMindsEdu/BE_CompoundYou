using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class FioraVictoriousEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "fiora-victorious";
    public override string TemplateId => "named.fiora-victorious";

    public override bool HasKeyword(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        string keyword
    )
    {
        if (runtime.GetEffectiveMight(session, card) < 5)
        {
            return false;
        }

        return string.Equals(keyword, "Deflect", StringComparison.OrdinalIgnoreCase)
            || string.Equals(keyword, "Ganking", StringComparison.OrdinalIgnoreCase)
            || string.Equals(keyword, "Shield", StringComparison.OrdinalIgnoreCase);
    }
}
