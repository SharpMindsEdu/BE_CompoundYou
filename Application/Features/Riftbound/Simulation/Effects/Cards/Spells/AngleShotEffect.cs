using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class AngleShotEffect : RiftboundNamedCardEffectBase
{
    private const string SourceUnitMarker = "-source-unit-";

    public override string NameIdentifier => "angle-shot";
    public override string TemplateId => "named.angle-shot";

    public override bool TryAddLegalActions(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        List<RiftboundLegalAction> actions
    )
    {
        var units = RiftboundEffectUnitTargeting.EnumerateAllUnits(session);
        var equipment = RiftboundEffectGearTargeting.EnumerateAllGear(session)
            .Where(IsEquipment)
            .ToList();

        foreach (var unit in units)
        {
            foreach (var gear in equipment.Where(x => x.ControllerPlayerIndex == unit.ControllerPlayerIndex))
            {
                actions.Add(
                    new RiftboundLegalAction(
                        $"{runtime.ActionPrefix}play-{card.InstanceId}-spell{SourceUnitMarker}{unit.InstanceId}-target-gear-{gear.InstanceId}",
                        RiftboundActionType.PlayCard,
                        player.PlayerIndex,
                        $"Play {card.Name} with {unit.Name} and {gear.Name}"
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
        var unit = ResolveSourceUnit(session, actionId);
        var gear = RiftboundEffectGearTargeting.ResolveTargetGearFromAction(session, actionId);
        if (unit is null || gear is null || unit.ControllerPlayerIndex != gear.ControllerPlayerIndex)
        {
            return;
        }

        var detached = false;
        if (gear.AttachedToInstanceId == unit.InstanceId)
        {
            gear.AttachedToInstanceId = null;
            detached = true;
        }
        else
        {
            RiftboundEffectGearTargeting.RemoveGearFromBoard(session, gear);
            gear.AttachedToInstanceId = unit.InstanceId;
            AddGearToUnitLocation(session, gear, unit);
            runtime.NotifyGearAttached(session, gear, unit);
        }

        runtime.DrawCards(player, 1);
        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "Resolve",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["unit"] = unit.Name,
                ["gear"] = gear.Name,
                ["detached"] = detached ? "true" : "false",
                ["drew"] = "1",
            }
        );
    }

    private static CardInstance? ResolveSourceUnit(GameSession session, string actionId)
    {
        var markerIndex = actionId.IndexOf(SourceUnitMarker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return null;
        }

        var fragment = actionId[(markerIndex + SourceUnitMarker.Length)..];
        var match = System.Text.RegularExpressions.Regex.Match(
            fragment,
            @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}"
        );
        if (!match.Success || !Guid.TryParse(match.Value, out var unitId))
        {
            return null;
        }

        return RiftboundEffectUnitTargeting.EnumerateAllUnits(session).FirstOrDefault(x =>
            x.InstanceId == unitId
        );
    }

    private static void AddGearToUnitLocation(GameSession session, CardInstance gear, CardInstance unit)
    {
        var ownerBase = session.Players.FirstOrDefault(x =>
            x.BaseZone.Cards.Any(c => c.InstanceId == unit.InstanceId)
        );
        if (ownerBase is not null)
        {
            ownerBase.BaseZone.Cards.Add(gear);
            return;
        }

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
