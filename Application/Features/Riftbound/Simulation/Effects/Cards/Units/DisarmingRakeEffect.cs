using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class DisarmingRakeEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "disarming-rake";
    public override string TemplateId => "named.disarming-rake";

    public override void OnUnitPlay(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        string actionId
    )
    {
        var gearToKill = RiftboundEffectGearTargeting.EnumerateAllGear(session)
            .OrderByDescending(x => x.Cost.GetValueOrDefault())
            .ThenBy(x => x.Name, StringComparer.Ordinal)
            .ThenBy(x => x.InstanceId)
            .FirstOrDefault();
        if (gearToKill is null)
        {
            return;
        }

        if (!RiftboundEffectGearTargeting.RemoveGearFromBoard(session, gearToKill))
        {
            return;
        }

        gearToKill.AttachedToInstanceId = null;
        var owner = RiftboundEffectGearTargeting.ResolveOwnerPlayer(session, gearToKill);
        owner.TrashZone.Cards.Add(gearToKill);
    }
}

