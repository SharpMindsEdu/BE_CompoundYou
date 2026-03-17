using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class TideturnerEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "tideturner";
    public override string TemplateId => "named.tideturner";

    public override void OnUnitPlay(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        string actionId
    )
    {
        var myLocation = ResolveLocation(session, card);
        if (myLocation is null)
        {
            return;
        }

        var target = RiftboundEffectUnitTargeting.EnumerateFriendlyUnits(session, player.PlayerIndex)
            .Where(x => x.InstanceId != card.InstanceId)
            .Select(x => new { Unit = x, Location = ResolveLocation(session, x) })
            .Where(x => x.Location is not null && !string.Equals(x.Location, myLocation, StringComparison.Ordinal))
            .OrderByDescending(x => x.Unit.Might.GetValueOrDefault())
            .ThenBy(x => x.Unit.Name, StringComparer.Ordinal)
            .ThenBy(x => x.Unit.InstanceId)
            .FirstOrDefault();
        if (target is null || string.IsNullOrWhiteSpace(target.Location))
        {
            return;
        }

        RemoveFromCurrentLocation(session, card);
        RemoveFromCurrentLocation(session, target.Unit);

        AddToLocation(session, card, target.Location!);
        AddToLocation(session, target.Unit, myLocation);

        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "WhenPlay",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["swappedWith"] = target.Unit.Name,
                ["from"] = myLocation,
                ["to"] = target.Location!,
            }
        );
    }

    private static string? ResolveLocation(GameSession session, CardInstance unit)
    {
        var baseOwner = session.Players.FirstOrDefault(x =>
            x.BaseZone.Cards.Any(card => card.InstanceId == unit.InstanceId)
        );
        if (baseOwner is not null)
        {
            return $"base-{baseOwner.PlayerIndex}";
        }

        var battlefield = RiftboundEffectUnitTargeting.FindBattlefieldContainingUnit(session, unit.InstanceId);
        return battlefield is null ? null : $"bf-{battlefield.Index}";
    }

    private static void RemoveFromCurrentLocation(GameSession session, CardInstance unit)
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

    private static void AddToLocation(GameSession session, CardInstance unit, string location)
    {
        if (location.StartsWith("base-", StringComparison.Ordinal))
        {
            if (int.TryParse(location["base-".Length..], out var baseOwnerIndex))
            {
                var owner = session.Players.FirstOrDefault(x => x.PlayerIndex == baseOwnerIndex);
                owner?.BaseZone.Cards.Add(unit);
            }

            return;
        }

        if (location.StartsWith("bf-", StringComparison.Ordinal) && int.TryParse(location["bf-".Length..], out var battlefieldIndex))
        {
            var battlefield = session.Battlefields.FirstOrDefault(x => x.Index == battlefieldIndex);
            battlefield?.Units.Add(unit);
        }
    }
}

