using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class AdaptatronEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "adaptatron";
    public override string TemplateId => "named.adaptatron";

    public override void OnConquer(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        BattlefieldState battlefield
    )
    {
        var gearToKill = RiftboundEffectGearTargeting.EnumerateAllGear(session)
            .OrderBy(x => x.ControllerPlayerIndex == player.PlayerIndex ? 1 : 0)
            .ThenByDescending(x => x.Cost.GetValueOrDefault())
            .ThenBy(x => x.Name, StringComparer.Ordinal)
            .ThenBy(x => x.InstanceId)
            .FirstOrDefault();
        if (gearToKill is null || !RiftboundEffectGearTargeting.RemoveGearFromBoard(session, gearToKill))
        {
            return;
        }

        gearToKill.AttachedToInstanceId = null;
        var owner = RiftboundEffectGearTargeting.ResolveOwnerPlayer(session, gearToKill);
        owner.TrashZone.Cards.Add(gearToKill);

        card.PermanentMightModifier += 1;
        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "WhenConquer",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["killedGear"] = gearToKill.Name,
                ["buffed"] = "true",
                ["battlefield"] = battlefield.Name,
            }
        );
    }
}
