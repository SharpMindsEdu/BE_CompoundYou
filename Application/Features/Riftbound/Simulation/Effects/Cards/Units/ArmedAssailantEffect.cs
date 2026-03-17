using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class ArmedAssailantEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "armed-assailant";
    public override string TemplateId => "named.armed-assailant";

    public override void OnUnitPlay(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        string actionId
    )
    {
        var gearToAttach = RiftboundEffectGearTargeting.EnumerateControlledGear(session, player.PlayerIndex)
            .Where(IsEquipment)
            .OrderByDescending(x => x.Cost.GetValueOrDefault())
            .ThenBy(x => x.Name, StringComparer.Ordinal)
            .ThenBy(x => x.InstanceId)
            .FirstOrDefault();
        if (gearToAttach is null)
        {
            return;
        }

        RiftboundEffectGearTargeting.RemoveGearFromBoard(session, gearToAttach);
        gearToAttach.AttachedToInstanceId = card.InstanceId;
        AddGearToUnitLocation(session, gearToAttach, card);
        runtime.NotifyGearAttached(session, gearToAttach, card);
        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "WhenPlay",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["equippedGear"] = gearToAttach.Name,
            }
        );
    }

    private static void AddGearToUnitLocation(GameSession session, CardInstance gear, CardInstance unit)
    {
        var battlefield = RiftboundEffectUnitTargeting.FindBattlefieldContainingUnit(session, unit.InstanceId);
        if (battlefield is not null)
        {
            battlefield.Gear.Add(gear);
            return;
        }

        session.Players[unit.ControllerPlayerIndex].BaseZone.Cards.Add(gear);
    }

    private static bool IsEquipment(CardInstance gear)
    {
        return gear.Keywords.Contains("Equip", StringComparer.OrdinalIgnoreCase)
            || string.Equals(gear.EffectTemplateId, "gear.attach-friendly-unit", StringComparison.Ordinal);
    }
}
