using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class EagerDrakehoundEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "eager-drakehound";
    public override string TemplateId => "named.eager-drakehound";

    public override void OnUnitPlay(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        string actionId
    )
    {
        card.IsExhausted = false;
    }
}

