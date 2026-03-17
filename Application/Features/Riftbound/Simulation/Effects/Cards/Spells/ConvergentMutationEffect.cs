using System.Text.RegularExpressions;
using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed partial class ConvergentMutationEffect : RiftboundNamedCardEffectBase
{
    private const string SourceUnitMarker = "-source-unit-";

    [GeneratedRegex(
        @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}",
        RegexOptions.Compiled
    )]
    private static partial Regex GuidRegex();

    public override string NameIdentifier => "convergent-mutation";
    public override string TemplateId => "named.convergent-mutation";

    public override bool TryAddLegalActions(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        List<RiftboundLegalAction> actions
    )
    {
        var friendly = RiftboundEffectUnitTargeting.EnumerateFriendlyUnits(session, player.PlayerIndex).ToList();
        foreach (var target in friendly)
        {
            foreach (var reference in friendly.Where(x => x.InstanceId != target.InstanceId))
            {
                actions.Add(
                    new RiftboundLegalAction(
                        $"{runtime.ActionPrefix}play-{card.InstanceId}-spell{SourceUnitMarker}{target.InstanceId}-target-unit-{reference.InstanceId}",
                        RiftboundActionType.PlayCard,
                        player.PlayerIndex,
                        $"Play {card.Name}: set {target.Name} to {reference.Name}"
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
        var target = ResolveSourceUnit(session, actionId);
        var reference = RiftboundEffectUnitTargeting.ResolveTargetUnitFromAction(session, actionId);
        if (
            target is null
            || reference is null
            || target.ControllerPlayerIndex != player.PlayerIndex
            || reference.ControllerPlayerIndex != player.PlayerIndex
            || target.InstanceId == reference.InstanceId
        )
        {
            return;
        }

        var targetMight = runtime.GetEffectiveMight(session, target);
        var referenceMight = runtime.GetEffectiveMight(session, reference);
        if (referenceMight <= targetMight)
        {
            return;
        }

        target.TemporaryMightModifier += referenceMight - targetMight;
    }

    private static CardInstance? ResolveSourceUnit(GameSession session, string actionId)
    {
        var markerIndex = actionId.IndexOf(SourceUnitMarker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return null;
        }

        var fragment = actionId[(markerIndex + SourceUnitMarker.Length)..];
        var match = GuidRegex().Match(fragment);
        if (!match.Success || !Guid.TryParse(match.Value, out var unitId))
        {
            return null;
        }

        return RiftboundEffectUnitTargeting.EnumerateAllUnits(session).FirstOrDefault(x =>
            x.InstanceId == unitId
        );
    }
}

