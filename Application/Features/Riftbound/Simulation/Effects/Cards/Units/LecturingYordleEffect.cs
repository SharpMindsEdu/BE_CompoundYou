using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class LecturingYordleEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "lecturing-yordle";
    public override string TemplateId => "named.lecturing-yordle";

    public override void OnUnitPlay(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        string actionId
    )
    {
        runtime.DrawCards(player, 1);
    }
}
