using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class MasterYiHonedEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "master-yi-honed";
    public override string TemplateId => "named.master-yi-honed";

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
