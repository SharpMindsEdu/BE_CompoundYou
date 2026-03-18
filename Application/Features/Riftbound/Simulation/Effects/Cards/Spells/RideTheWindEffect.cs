using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class RideTheWindEffect : RiftboundNamedCardEffectBase
{
    private const string MoveUnitMarker = "-move-unit-";
    private const string ToBaseMarker = "-to-base";
    private const string ToBattlefieldMarker = "-to-bf-";

    public override string NameIdentifier => "ride-the-wind";
    public override string TemplateId => "named.ride-the-wind";

    public override bool TryAddLegalActions(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        List<RiftboundLegalAction> actions
    )
    {
        var canMoveToBase = !RiftboundEffectUnitTargeting.IsMoveToBaseLocked(session);
        foreach (
            var unit in RiftboundEffectUnitTargeting.EnumerateFriendlyUnits(session, player.PlayerIndex)
        )
        {
            var currentBattlefield = RiftboundEffectUnitTargeting.FindBattlefieldContainingUnit(
                session,
                unit.InstanceId
            );
            var isInBase = currentBattlefield is null;

            if (!isInBase && canMoveToBase)
            {
                actions.Add(
                    new RiftboundLegalAction(
                        $"{runtime.ActionPrefix}play-{card.InstanceId}-spell{MoveUnitMarker}{unit.InstanceId}{ToBaseMarker}",
                        RiftboundActionType.PlayCard,
                        player.PlayerIndex,
                        $"Play {card.Name} moving {unit.Name} to base"
                    )
                );
            }

            foreach (var battlefield in session.Battlefields)
            {
                if (currentBattlefield is not null && battlefield.Index == currentBattlefield.Index)
                {
                    continue;
                }

                actions.Add(
                    new RiftboundLegalAction(
                        $"{runtime.ActionPrefix}play-{card.InstanceId}-spell{MoveUnitMarker}{unit.InstanceId}{ToBattlefieldMarker}{battlefield.Index}",
                        RiftboundActionType.PlayCard,
                        player.PlayerIndex,
                        $"Play {card.Name} moving {unit.Name} to battlefield {battlefield.Name}"
                    )
                );
            }
        }

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
        var unit = ResolveMoveTargetUnit(session, actionId);
        if (unit is null || unit.ControllerPlayerIndex != player.PlayerIndex)
        {
            return;
        }

        RemoveFromCurrentLocation(session, unit);

        if (actionId.Contains(ToBaseMarker, StringComparison.Ordinal))
        {
            if (RiftboundEffectUnitTargeting.IsMoveToBaseLocked(session))
            {
                return;
            }

            player.BaseZone.Cards.Add(unit);
        }
        else
        {
            var battlefieldIndex = ResolveBattlefieldIndex(actionId);
            if (!battlefieldIndex.HasValue)
            {
                return;
            }

            var destination = session.Battlefields.FirstOrDefault(x => x.Index == battlefieldIndex.Value);
            if (destination is null)
            {
                return;
            }

            destination.Units.Add(unit);
            if (destination.ControlledByPlayerIndex != player.PlayerIndex)
            {
                destination.ContestedByPlayerIndex = player.PlayerIndex;
            }
        }

        unit.IsExhausted = false;

        var drawCount = runtime.ReadIntEffectData(unit, "onMove.loot", fallback: 0);
        if (drawCount > 0)
        {
            if (player.HandZone.Cards.Count > 0)
            {
                var discard = player.HandZone.Cards
                    .OrderBy(x => x.Cost.GetValueOrDefault())
                    .ThenBy(x => x.Name, StringComparer.Ordinal)
                    .ThenBy(x => x.InstanceId)
                    .First();
                runtime.DiscardFromHand(
                    session,
                    player,
                    discard,
                    reason: "RideTheWindLoot",
                    sourceCard: card
                );
            }

            runtime.DrawCards(player, drawCount);
        }

        var goldTokenCount = runtime.ReadIntEffectData(unit, "onMove.playGoldToken", fallback: 0);
        for (var i = 0; i < goldTokenCount; i += 1)
        {
            player.BaseZone.Cards.Add(
                RiftboundTokenFactory.CreateGoldGearToken(
                    ownerPlayerIndex: player.PlayerIndex,
                    controllerPlayerIndex: player.PlayerIndex,
                    exhausted: true
                )
            );
        }

        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "Resolve",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["target"] = unit.Name,
                ["ready"] = "true",
            }
        );
    }

    private static CardInstance? ResolveMoveTargetUnit(GameSession session, string actionId)
    {
        var markerIndex = actionId.IndexOf(MoveUnitMarker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return null;
        }

        var fragment = actionId[(markerIndex + MoveUnitMarker.Length)..];
        var match = System.Text.RegularExpressions.Regex.Match(
            fragment,
            @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}"
        );
        if (!match.Success || !Guid.TryParse(match.Value, out var unitId))
        {
            return null;
        }

        return RiftboundEffectUnitTargeting.EnumerateAllUnits(session)
            .FirstOrDefault(x => x.InstanceId == unitId);
    }

    private static int? ResolveBattlefieldIndex(string actionId)
    {
        var markerIndex = actionId.IndexOf(ToBattlefieldMarker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return null;
        }

        var fragment = actionId[(markerIndex + ToBattlefieldMarker.Length)..];
        return int.TryParse(fragment, out var index) ? index : null;
    }

    private static void RemoveFromCurrentLocation(GameSession session, CardInstance unit)
    {
        foreach (var owner in session.Players)
        {
            if (owner.BaseZone.Cards.Remove(unit))
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
