using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class GemJammerEffect : RiftboundNamedCardEffectBase
{
    private const string TargetMarker = "-gem-jammer-target-unit-";

    public override string NameIdentifier => "gem-jammer";
    public override string TemplateId => "named.gem-jammer";

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

        var friendlyTargets = RiftboundEffectUnitTargeting.EnumerateFriendlyUnits(session, player.PlayerIndex)
            .Where(x => x.InstanceId != card.InstanceId)
            .ToList();
        if (friendlyTargets.Count == 0)
        {
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

            return true;
        }

        foreach (var destination in destinations)
        {
            foreach (var target in friendlyTargets)
            {
                actions.Add(
                    new RiftboundLegalAction(
                        $"{runtime.ActionPrefix}play-{card.InstanceId}{TargetMarker}{target.InstanceId}{destination.Suffix}",
                        RiftboundActionType.PlayCard,
                        player.PlayerIndex,
                        $"{destination.Description} (grant Ganking to {target.Name})"
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
        var target = ResolveTarget(session, actionId);
        if (target is null || target.ControllerPlayerIndex != player.PlayerIndex)
        {
            return;
        }

        var granted = false;
        if (!target.Keywords.Contains("Ganking", StringComparer.OrdinalIgnoreCase))
        {
            target.Keywords.Add("Ganking");
            granted = true;
        }

        if (granted)
        {
            target.EffectData["temporaryGankingGranted"] = "true";
        }

        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "WhenPlay",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["target"] = target.Name,
                ["grantedKeyword"] = "Ganking",
                ["temporary"] = granted.ToString().ToLowerInvariant(),
            }
        );
    }

    private static CardInstance? ResolveTarget(GameSession session, string actionId)
    {
        var markerIndex = actionId.IndexOf(TargetMarker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return null;
        }

        var fragment = actionId[(markerIndex + TargetMarker.Length)..];
        var guidText = fragment.Length >= 36 ? fragment[..36] : string.Empty;
        if (!Guid.TryParse(guidText, out var targetId))
        {
            return null;
        }

        return RiftboundEffectUnitTargeting.EnumerateAllUnits(session)
            .FirstOrDefault(x => x.InstanceId == targetId);
    }
}
