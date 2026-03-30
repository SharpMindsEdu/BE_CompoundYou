using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class JaxGrandmasterAtArmsEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "jax-grandmaster-at-arms";
    public override string TemplateId => "named.jax-grandmaster-at-arms";
    public override bool HasActivatedAbility => true;

    public override bool TryActivateAbility(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card
    )
    {
        if (card.IsExhausted)
        {
            return false;
        }

        var targetUnit = RiftboundEffectUnitTargeting.EnumerateFriendlyUnits(session, player.PlayerIndex)
            .OrderByDescending(x => runtime.GetEffectiveMight(session, x))
            .ThenBy(x => x.Name, StringComparer.Ordinal)
            .ThenBy(x => x.InstanceId)
            .FirstOrDefault();
        if (targetUnit is null)
        {
            return false;
        }

        var detachedEquipment = RiftboundEffectGearTargeting.EnumerateControlledGear(
                session,
                player.PlayerIndex
            )
            .Where(IsEquipmentCard)
            .Where(x => x.AttachedToInstanceId is null)
            .OrderByDescending(x => x.Cost.GetValueOrDefault())
            .ThenBy(x => x.Name, StringComparer.Ordinal)
            .ThenBy(x => x.InstanceId)
            .FirstOrDefault();
        if (detachedEquipment is not null)
        {
            if (!runtime.TryPayCost(session, player, energyCost: 1))
            {
                return false;
            }

            card.IsExhausted = true;
            AttachToUnit(session, player, detachedEquipment, targetUnit);
            runtime.NotifyGearAttached(session, detachedEquipment, targetUnit);
            runtime.AddEffectContext(
                session,
                card.Name,
                player.PlayerIndex,
                "Activate",
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["template"] = card.EffectTemplateId,
                    ["mode"] = "detached",
                    ["targetUnit"] = targetUnit.Name,
                    ["equipment"] = detachedEquipment.Name,
                    ["paidEnergy"] = "1",
                }
            );
            return true;
        }

        var attachedEquipment = RiftboundEffectGearTargeting.EnumerateControlledGear(
                session,
                player.PlayerIndex
            )
            .Where(IsEquipmentCard)
            .Where(x => x.AttachedToInstanceId is not null)
            .OrderByDescending(x => x.Cost.GetValueOrDefault())
            .ThenBy(x => x.Name, StringComparer.Ordinal)
            .ThenBy(x => x.InstanceId)
            .FirstOrDefault();
        if (attachedEquipment is null)
        {
            return false;
        }

        card.IsExhausted = true;
        AttachToUnit(session, player, attachedEquipment, targetUnit);
        runtime.NotifyGearAttached(session, attachedEquipment, targetUnit);
        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "Activate",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["mode"] = "attached",
                ["targetUnit"] = targetUnit.Name,
                ["equipment"] = attachedEquipment.Name,
                ["paidEnergy"] = "0",
            }
        );
        return true;
    }

    private static bool IsEquipmentCard(CardInstance card)
    {
        return string.Equals(card.Type, "Gear", StringComparison.OrdinalIgnoreCase)
            && (
                card.Keywords.Contains("Equip", StringComparer.OrdinalIgnoreCase)
                || card.Keywords.Contains("Equipment", StringComparer.OrdinalIgnoreCase)
            );
    }

    private static void AttachToUnit(
        GameSession session,
        PlayerState player,
        CardInstance gear,
        CardInstance targetUnit
    )
    {
        RiftboundEffectGearTargeting.RemoveGearFromBoard(session, gear);
        gear.AttachedToInstanceId = targetUnit.InstanceId;
        var destinationBattlefield = RiftboundEffectUnitTargeting.FindBattlefieldContainingUnit(
            session,
            targetUnit.InstanceId
        );
        if (destinationBattlefield is not null)
        {
            destinationBattlefield.Gear.Add(gear);
        }
        else
        {
            player.BaseZone.Cards.Add(gear);
        }
    }
}
