using System.Globalization;
using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class DangerousDuoEffect : RiftboundNamedCardEffectBase
{
    public const string TargetMarker = "-dangerous-duo-target-";

    public override string NameIdentifier => "dangerous-duo";
    public override string TemplateId => "named.dangerous-duo";

    public override bool TryAddUnitPlayLegalActions(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        List<RiftboundLegalAction> actions
    )
    {
        var destinations = new List<(string Suffix, string Description)>
        {
            ("-to-base", $"Play {card.Name} to base"),
        };
        destinations.AddRange(
            session.Battlefields
                .Where(x => x.ControlledByPlayerIndex == player.PlayerIndex)
                .OrderBy(x => x.Index)
                .Select(x => ($"-to-bf-{x.Index}", $"Play {card.Name} to battlefield {x.Name}"))
        );

        foreach (var destination in destinations)
        {
            actions.Add(
                new RiftboundLegalAction(
                    $"{runtime.ActionPrefix}play-{card.InstanceId}{destination.Suffix}",
                    RiftboundActionType.PlayCard,
                    player.PlayerIndex,
                    destination.Description
                )
            );
        }

        if (!HasPlayedCardsThisTurn(session, player.PlayerIndex, minimumCount: 1))
        {
            return true;
        }

        foreach (var unit in RiftboundEffectUnitTargeting.EnumerateAllUnits(session))
        {
            foreach (var destination in destinations)
            {
                actions.Add(
                    new RiftboundLegalAction(
                        $"{runtime.ActionPrefix}play-{card.InstanceId}{TargetMarker}{unit.InstanceId}{destination.Suffix}",
                        RiftboundActionType.PlayCard,
                        player.PlayerIndex,
                        $"{destination.Description} (+2 [Might] to {unit.Name})"
                    )
                );
            }
        }

        return true;
    }

    public override void OnUnitPlay(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        string actionId
    )
    {
        if (!HasPlayedCardsThisTurn(session, player.PlayerIndex, minimumCount: 2))
        {
            return;
        }

        var markerIndex = actionId.IndexOf(TargetMarker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return;
        }

        var fragment = actionId[(markerIndex + TargetMarker.Length)..];
        var guidText = fragment.Length >= 36 ? fragment[..36] : string.Empty;
        if (!Guid.TryParse(guidText, out var targetId))
        {
            return;
        }

        var target = RiftboundEffectUnitTargeting.EnumerateAllUnits(session)
            .FirstOrDefault(x => x.InstanceId == targetId);
        if (target is null)
        {
            return;
        }

        target.TemporaryMightModifier += 2;
    }

    private static bool HasPlayedCardsThisTurn(
        GameSession session,
        int playerIndex,
        int minimumCount
    )
    {
        return session.EffectContexts.Count(x =>
            x.ControllerPlayerIndex == playerIndex
            && string.Equals(x.Timing, "Play", StringComparison.OrdinalIgnoreCase)
            && x.Metadata.TryGetValue("turn", out var turn)
            && int.TryParse(turn, NumberStyles.Integer, CultureInfo.InvariantCulture, out var playedTurn)
            && playedTurn == session.TurnNumber) >= minimumCount;
    }
}
