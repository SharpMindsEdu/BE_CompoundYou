using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class DivineJudgmentEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "divine-judgment";
    public override string TemplateId => "named.divine-judgment";

    public override bool TryAddLegalActions(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        List<RiftboundLegalAction> actions
    )
    {
        actions.Add(
            new RiftboundLegalAction(
                $"{runtime.ActionPrefix}play-{card.InstanceId}-spell",
                RiftboundActionType.PlayCard,
                player.PlayerIndex,
                $"Play spell {card.Name}"
            )
        );
        return true;
    }

    public override void OnSpellOrGearPlay(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        string actionId
    )
    {
        foreach (var currentPlayer in session.Players)
        {
            RecycleUnits(session, currentPlayer, keepCount: 2);
            RecycleGear(session, currentPlayer, keepCount: 2);
            RecycleRunes(currentPlayer, keepCount: 2);
            RecycleHand(currentPlayer, keepCount: 2);
        }
    }

    private static void RecycleUnits(GameSession session, PlayerState player, int keepCount)
    {
        var allControlledUnits = player.BaseZone.Cards
            .Where(x => string.Equals(x.Type, "Unit", StringComparison.OrdinalIgnoreCase))
            .Concat(
                session.Battlefields.SelectMany(x => x.Units).Where(x => x.ControllerPlayerIndex == player.PlayerIndex)
            )
            .OrderByDescending(x => x.Might.GetValueOrDefault() + x.PermanentMightModifier + x.TemporaryMightModifier)
            .ThenBy(x => x.Name, StringComparer.Ordinal)
            .ThenBy(x => x.InstanceId)
            .ToList();
        var keep = allControlledUnits.Take(keepCount).Select(x => x.InstanceId).ToHashSet();
        foreach (var unit in allControlledUnits.Where(x => !keep.Contains(x.InstanceId)))
        {
            RemoveUnitFromLocation(session, unit);
            player.TrashZone.Cards.Add(unit);
        }
    }

    private static void RecycleGear(GameSession session, PlayerState player, int keepCount)
    {
        var controlledGear = player.BaseZone.Cards
            .Where(x => string.Equals(x.Type, "Gear", StringComparison.OrdinalIgnoreCase))
            .Concat(
                session.Battlefields.SelectMany(x => x.Gear).Where(x => x.ControllerPlayerIndex == player.PlayerIndex)
            )
            .OrderByDescending(x => x.Cost.GetValueOrDefault())
            .ThenBy(x => x.Name, StringComparer.Ordinal)
            .ThenBy(x => x.InstanceId)
            .ToList();
        var keep = controlledGear.Take(keepCount).Select(x => x.InstanceId).ToHashSet();
        foreach (var gear in controlledGear.Where(x => !keep.Contains(x.InstanceId)))
        {
            if (!RiftboundEffectGearTargeting.RemoveGearFromBoard(session, gear))
            {
                continue;
            }

            gear.AttachedToInstanceId = null;
            player.TrashZone.Cards.Add(gear);
        }
    }

    private static void RecycleRunes(PlayerState player, int keepCount)
    {
        var runes = player.BaseZone.Cards
            .Where(x => string.Equals(x.Type, "Rune", StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Name, StringComparer.Ordinal)
            .ThenBy(x => x.InstanceId)
            .ToList();
        var keep = runes.Take(keepCount).Select(x => x.InstanceId).ToHashSet();
        foreach (var rune in runes.Where(x => !keep.Contains(x.InstanceId)))
        {
            player.BaseZone.Cards.Remove(rune);
            player.TrashZone.Cards.Add(rune);
        }
    }

    private static void RecycleHand(PlayerState player, int keepCount)
    {
        var hand = player.HandZone.Cards
            .OrderByDescending(x => x.Cost.GetValueOrDefault())
            .ThenBy(x => x.Name, StringComparer.Ordinal)
            .ThenBy(x => x.InstanceId)
            .ToList();
        var keep = hand.Take(keepCount).Select(x => x.InstanceId).ToHashSet();
        foreach (var card in hand.Where(x => !keep.Contains(x.InstanceId)))
        {
            player.HandZone.Cards.Remove(card);
            player.TrashZone.Cards.Add(card);
        }
    }

    private static void RemoveUnitFromLocation(GameSession session, CardInstance unit)
    {
        foreach (var currentPlayer in session.Players)
        {
            if (currentPlayer.BaseZone.Cards.Remove(unit))
            {
                return;
            }
        }

        foreach (var battlefield in session.Battlefields)
        {
            if (battlefield.Units.Remove(unit))
            {
                return;
            }
        }
    }
}

