using System.Text.RegularExpressions;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

internal static partial class RiftboundEffectGearTargeting
{
    private const string TargetGearMarker = "-target-gear-";

    [GeneratedRegex(
        @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}",
        RegexOptions.Compiled
    )]
    private static partial Regex GuidRegex();

    public static IReadOnlyCollection<CardInstance> EnumerateAllGear(GameSession session)
    {
        var gear = session.Battlefields.SelectMany(x => x.Gear).ToList();
        gear.AddRange(session.Players.SelectMany(x => x.BaseZone.Cards).Where(IsGearCard));
        return gear;
    }

    public static IReadOnlyCollection<CardInstance> EnumerateControlledGear(
        GameSession session,
        int playerIndex
    )
    {
        return EnumerateAllGear(session)
            .Where(x => x.ControllerPlayerIndex == playerIndex)
            .OrderBy(x => x.Name, StringComparer.Ordinal)
            .ThenBy(x => x.InstanceId)
            .ToList();
    }

    public static CardInstance? ResolveTargetGearFromAction(GameSession session, string actionId)
    {
        var markerIndex = actionId.IndexOf(TargetGearMarker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return null;
        }

        var fragment = actionId[(markerIndex + TargetGearMarker.Length)..];
        var match = GuidRegex().Match(fragment);
        if (!match.Success || !Guid.TryParse(match.Value, out var gearId))
        {
            return null;
        }

        return EnumerateAllGear(session).FirstOrDefault(x => x.InstanceId == gearId);
    }

    public static bool RemoveGearFromBoard(GameSession session, CardInstance gear)
    {
        foreach (var player in session.Players)
        {
            var removed = player.BaseZone.Cards.RemoveAll(x => x.InstanceId == gear.InstanceId);
            if (removed > 0)
            {
                return true;
            }
        }

        foreach (var battlefield in session.Battlefields)
        {
            var removed = battlefield.Gear.RemoveAll(x => x.InstanceId == gear.InstanceId);
            if (removed > 0)
            {
                return true;
            }
        }

        return false;
    }

    public static PlayerState ResolveOwnerPlayer(GameSession session, CardInstance gear)
    {
        return session.Players.FirstOrDefault(x => x.PlayerIndex == gear.OwnerPlayerIndex)
            ?? session.Players[0];
    }

    private static bool IsGearCard(CardInstance card)
    {
        return string.Equals(card.Type, "Gear", StringComparison.OrdinalIgnoreCase);
    }
}
