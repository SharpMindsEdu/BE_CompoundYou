using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class CombatChefEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "combat-chef";
    public override string TemplateId => "named.combat-chef";

    public override void OnUnitPlay(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        string actionId
    )
    {
        var equipment = RiftboundEffectGearTargeting.EnumerateControlledGear(session, player.PlayerIndex)
            .Where(x =>
                x.InstanceId != card.InstanceId && x.Keywords.Contains("Equip", StringComparer.OrdinalIgnoreCase)
            )
            .OrderByDescending(x => x.Cost.GetValueOrDefault())
            .ThenBy(x => x.Name, StringComparer.Ordinal)
            .ThenBy(x => x.InstanceId)
            .FirstOrDefault();
        if (equipment is null)
        {
            return;
        }

        if (!RiftboundEffectGearTargeting.RemoveGearFromBoard(session, equipment))
        {
            return;
        }

        equipment.AttachedToInstanceId = card.InstanceId;
        AddGearToChefLocation(session, player, card, equipment);
        runtime.NotifyGearAttached(session, equipment, card);
    }

    private static void AddGearToChefLocation(
        GameSession session,
        PlayerState player,
        CardInstance chef,
        CardInstance gear
    )
    {
        var battlefield = RiftboundEffectUnitTargeting.FindBattlefieldContainingUnit(session, chef.InstanceId);
        if (battlefield is not null)
        {
            battlefield.Gear.Add(gear);
            return;
        }

        player.BaseZone.Cards.Add(gear);
    }
}

