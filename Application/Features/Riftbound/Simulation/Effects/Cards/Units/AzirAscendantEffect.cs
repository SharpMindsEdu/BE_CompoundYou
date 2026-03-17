using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class AzirAscendantEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "azir-ascendant";
    public override string TemplateId => "named.azir-ascendant";
    public override bool HasActivatedAbility => true;

    public override bool TryActivateAbility(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card
    )
    {
        if (HasActivatedThisTurn(session, card))
        {
            return false;
        }

        if (!runtime.TryPayCost(session, player, energyCost: 0, [new EffectPowerRequirement(1, ["Calm"])]))
        {
            return false;
        }

        var target = RiftboundEffectUnitTargeting.EnumerateFriendlyUnits(session, player.PlayerIndex)
            .Where(x => x.InstanceId != card.InstanceId)
            .OrderByDescending(x => x.Might.GetValueOrDefault())
            .ThenBy(x => x.Name, StringComparer.Ordinal)
            .ThenBy(x => x.InstanceId)
            .FirstOrDefault();
        if (target is null)
        {
            return false;
        }

        var targetBattlefield = RiftboundEffectUnitTargeting.FindBattlefieldContainingUnit(
            session,
            target.InstanceId
        );
        var azirBattlefield = RiftboundEffectUnitTargeting.FindBattlefieldContainingUnit(session, card.InstanceId);
        var targetInBase = player.BaseZone.Cards.Any(x => x.InstanceId == target.InstanceId);
        var azirInBase = player.BaseZone.Cards.Any(x => x.InstanceId == card.InstanceId);
        if ((!targetInBase && targetBattlefield is null) || (!azirInBase && azirBattlefield is null))
        {
            return false;
        }

        RemoveUnitFromCurrentLocation(session, player, target);
        RemoveUnitFromCurrentLocation(session, player, card);
        AddUnitToOriginalLocation(player, targetBattlefield, card);
        AddUnitToOriginalLocation(player, azirBattlefield, target);

        var movedEquipment = MoveEquipmentFromTargetToAzir(session, player, target, card);
        if (movedEquipment is not null)
        {
            runtime.NotifyGearAttached(session, movedEquipment, card);
        }
        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "Activate",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["swappedWith"] = target.Name,
                ["movedEquipment"] = movedEquipment?.Name ?? string.Empty,
                ["azirAscendantSwapUsed"] = "true",
                ["instanceId"] = card.InstanceId.ToString(),
            }
        );

        return true;
    }

    private static bool HasActivatedThisTurn(GameSession session, CardInstance card)
    {
        return session.EffectContexts.Any(context =>
            string.Equals(context.Source, card.Name, StringComparison.OrdinalIgnoreCase)
            && string.Equals(context.Timing, "Activate", StringComparison.OrdinalIgnoreCase)
            && context.Metadata.TryGetValue("azirAscendantSwapUsed", out var used)
            && bool.TryParse(used, out var isUsed)
            && isUsed
            && context.Metadata.TryGetValue("instanceId", out var instanceId)
            && string.Equals(instanceId, card.InstanceId.ToString(), StringComparison.OrdinalIgnoreCase)
            && context.Metadata.TryGetValue("turn", out var turnText)
            && int.TryParse(turnText, out var turn)
            && turn == session.TurnNumber
        );
    }

    private static void RemoveUnitFromCurrentLocation(
        GameSession session,
        PlayerState player,
        CardInstance unit
    )
    {
        if (player.BaseZone.Cards.Remove(unit))
        {
            return;
        }

        foreach (var battlefield in session.Battlefields)
        {
            if (battlefield.Units.Remove(unit))
            {
                return;
            }
        }
    }

    private static void AddUnitToOriginalLocation(
        PlayerState player,
        BattlefieldState? originalBattlefield,
        CardInstance unit
    )
    {
        if (originalBattlefield is not null)
        {
            originalBattlefield.Units.Add(unit);
            return;
        }

        player.BaseZone.Cards.Add(unit);
    }

    private static CardInstance? MoveEquipmentFromTargetToAzir(
        GameSession session,
        PlayerState player,
        CardInstance fromUnit,
        CardInstance azir
    )
    {
        var equipment = RiftboundEffectGearTargeting.EnumerateControlledGear(session, player.PlayerIndex)
            .Where(x => x.AttachedToInstanceId == fromUnit.InstanceId)
            .OrderByDescending(x => x.Cost.GetValueOrDefault())
            .ThenBy(x => x.Name, StringComparer.Ordinal)
            .ThenBy(x => x.InstanceId)
            .FirstOrDefault();
        if (equipment is null)
        {
            return null;
        }

        if (!RiftboundEffectGearTargeting.RemoveGearFromBoard(session, equipment))
        {
            return null;
        }

        equipment.AttachedToInstanceId = azir.InstanceId;
        var battlefield = RiftboundEffectUnitTargeting.FindBattlefieldContainingUnit(session, azir.InstanceId);
        if (battlefield is null)
        {
            player.BaseZone.Cards.Add(equipment);
        }
        else
        {
            battlefield.Gear.Add(equipment);
        }

        return equipment;
    }
}
