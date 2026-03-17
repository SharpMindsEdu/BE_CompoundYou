using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class DirewingEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "direwing";
    public override string TemplateId => "named.direwing";

    public override void OnUnitPlay(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        string actionId
    )
    {
        var hasAnotherDragon = RiftboundEffectUnitTargeting.EnumerateFriendlyUnits(session, player.PlayerIndex)
            .Any(x =>
                x.InstanceId != card.InstanceId
                && x.Keywords.Contains("Dragon", StringComparer.OrdinalIgnoreCase));
        if (hasAnotherDragon)
        {
            card.IsExhausted = false;
        }
    }
}

