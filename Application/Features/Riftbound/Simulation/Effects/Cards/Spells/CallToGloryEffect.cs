using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class CallToGloryEffect : RiftboundNamedCardEffectBase
{
    public const string SpendBuffMarker = "-call-to-glory-spend-buff-";

    public override string NameIdentifier => "call-to-glory";
    public override string TemplateId => "named.call-to-glory";

    public override bool TryAddLegalActions(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        List<RiftboundLegalAction> actions
    )
    {
        var targets = RiftboundEffectUnitTargeting.EnumerateAllUnits(session).ToList();
        var buffSources = RiftboundEffectUnitTargeting.EnumerateFriendlyUnits(session, player.PlayerIndex)
            .Where(x => x.PermanentMightModifier > 0)
            .ToList();
        foreach (var target in targets)
        {
            actions.Add(
                new RiftboundLegalAction(
                    $"{runtime.ActionPrefix}play-{card.InstanceId}-spell-target-unit-{target.InstanceId}",
                    RiftboundActionType.PlayCard,
                    player.PlayerIndex,
                    $"Play {card.Name} targeting {target.Name}"
                )
            );

            foreach (var source in buffSources)
            {
                actions.Add(
                    new RiftboundLegalAction(
                        $"{runtime.ActionPrefix}play-{card.InstanceId}-spell-target-unit-{target.InstanceId}{SpendBuffMarker}{source.InstanceId}",
                        RiftboundActionType.PlayCard,
                        player.PlayerIndex,
                        $"Play {card.Name} targeting {target.Name} (spend buff from {source.Name})"
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
        var target = RiftboundEffectUnitTargeting.ResolveTargetUnitFromAction(session, actionId);
        if (target is null)
        {
            return;
        }

        target.TemporaryMightModifier += 3;
        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "Resolve",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["target"] = target.Name,
                ["magnitude"] = "3",
                ["spentBuff"] = actionId.Contains(SpendBuffMarker, StringComparison.Ordinal)
                    ? "true"
                    : "false",
            }
        );
    }
}
