using System.Text.RegularExpressions;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

internal static partial class RiftboundEffectUnitTargeting
{
    private const string TargetUnitMarker = "-target-unit-";
    private const string RepeatSuffix = "-repeat";

    [GeneratedRegex(
        @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}",
        RegexOptions.Compiled
    )]
    private static partial Regex GuidRegex();

    public static IReadOnlyCollection<CardInstance> EnumerateAllUnits(GameSession session)
    {
        var units = session.Battlefields.SelectMany(x => x.Units).ToList();
        units.AddRange(
            session.Players.SelectMany(x => x.BaseZone.Cards).Where(IsUnitCard)
        );
        return units;
    }

    public static IReadOnlyCollection<CardInstance> EnumerateFriendlyUnits(
        GameSession session,
        int playerIndex
    )
    {
        return EnumerateAllUnits(session)
            .Where(x => x.ControllerPlayerIndex == playerIndex)
            .OrderBy(x => x.Name, StringComparer.Ordinal)
            .ThenBy(x => x.InstanceId)
            .ToList();
    }

    public static CardInstance? ResolveTargetUnitFromAction(GameSession session, string actionId)
    {
        var markerIndex = actionId.IndexOf(TargetUnitMarker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return null;
        }

        var fragment = actionId[(markerIndex + TargetUnitMarker.Length)..];
        if (fragment.EndsWith(RepeatSuffix, StringComparison.Ordinal))
        {
            fragment = fragment[..^RepeatSuffix.Length];
        }

        var match = GuidRegex().Match(fragment);
        if (!match.Success || !Guid.TryParse(match.Value, out var unitId))
        {
            return null;
        }

        return EnumerateAllUnits(session).FirstOrDefault(x => x.InstanceId == unitId);
    }

    public static int CountFriendlyUnitsAtSameLocation(
        GameSession session,
        CardInstance targetUnit,
        int playerIndex
    )
    {
        foreach (var player in session.Players)
        {
            if (player.BaseZone.Cards.Any(x => x.InstanceId == targetUnit.InstanceId && IsUnitCard(x)))
            {
                return player.BaseZone.Cards.Count(x =>
                    IsUnitCard(x) && x.ControllerPlayerIndex == playerIndex
                );
            }
        }

        foreach (var battlefield in session.Battlefields)
        {
            if (battlefield.Units.Any(x => x.InstanceId == targetUnit.InstanceId))
            {
                return battlefield.Units.Count(x => x.ControllerPlayerIndex == playerIndex);
            }
        }

        return 0;
    }

    private static bool IsUnitCard(CardInstance card)
    {
        return string.Equals(card.Type, "Unit", StringComparison.OrdinalIgnoreCase);
    }
}
