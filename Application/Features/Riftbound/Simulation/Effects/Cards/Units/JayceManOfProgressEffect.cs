using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class JayceManOfProgressEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "jayce-man-of-progress";
    public override string TemplateId => "named.jayce-man-of-progress";

    public override void OnUnitPlay(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        string actionId
    )
    {
        var gearToKill = RiftboundEffectGearTargeting.EnumerateControlledGear(
                session,
                player.PlayerIndex
            )
            .OrderByDescending(x => x.Cost.GetValueOrDefault())
            .ThenBy(x => x.Name, StringComparer.Ordinal)
            .ThenBy(x => x.InstanceId)
            .FirstOrDefault();
        if (gearToKill is null)
        {
            return;
        }

        RiftboundEffectGearTargeting.RemoveGearFromBoard(session, gearToKill);
        gearToKill.AttachedToInstanceId = null;
        var owner = session.Players.FirstOrDefault(x => x.PlayerIndex == gearToKill.OwnerPlayerIndex);
        if (owner is null)
        {
            return;
        }

        owner.TrashZone.Cards.Add(gearToKill);
        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "Aura",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["killedFriendlyGear"] = gearToKill.Name,
                ["jayceIgnoreEnergyMaxCost"] = "7",
            }
        );
    }
}
