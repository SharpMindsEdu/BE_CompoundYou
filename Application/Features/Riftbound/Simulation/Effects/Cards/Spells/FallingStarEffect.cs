using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class FallingStarEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "falling-star";
    public override string TemplateId => "named.falling-star";

    protected override IReadOnlyDictionary<string, string> BuildData(
        RiftboundCard card,
        string normalizedEffectText
    )
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["magnitude"] = (RiftboundEffectTextParser.TryExtractMagnitude(normalizedEffectText) ?? 3).ToString(),
            ["hitCount"] = "2",
            ["resolveOnChainFinalize"] = "true",
        };
    }

    public override bool TryAddLegalActions(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        List<RiftboundLegalAction> actions
    )
    {
        var units = RiftboundEffectUnitTargeting.EnumerateAllUnits(session).ToList();
        foreach (var target in units)
        {
            actions.Add(
                new RiftboundLegalAction(
                    $"{runtime.ActionPrefix}play-{card.InstanceId}-spell-target-unit-{target.InstanceId}",
                    RiftboundActionType.PlayCard,
                    player.PlayerIndex,
                    $"Play {card.Name} targeting {target.Name}"
                )
            );
        }

        for (var left = 0; left < units.Count; left += 1)
        {
            for (var right = left + 1; right < units.Count; right += 1)
            {
                var targetList = string.Join(
                    ',',
                    new[] { units[left].InstanceId.ToString(), units[right].InstanceId.ToString() }
                );
                actions.Add(
                    new RiftboundLegalAction(
                        $"{runtime.ActionPrefix}play-{card.InstanceId}-spell{runtime.MultiTargetUnitsMarker}{targetList}",
                        RiftboundActionType.PlayCard,
                        player.PlayerIndex,
                        $"Play {card.Name} targeting {units[left].Name} and {units[right].Name}"
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
        var magnitude = runtime.ReadMagnitude(card, fallback: 3);
        var selectedTargets = RiftboundEffectUnitTargeting.ResolveTargetUnitsFromAction(session, actionId)
            .Take(2)
            .ToList();
        if (selectedTargets.Count == 2)
        {
            foreach (var selected in selectedTargets)
            {
                selected.MarkedDamage += magnitude;
            }

            runtime.AddEffectContext(
                session,
                card.Name,
                player.PlayerIndex,
                "Resolve",
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["template"] = card.EffectTemplateId,
                    ["target"] = string.Join(", ", selectedTargets.Select(x => x.Name)),
                    ["magnitude"] = magnitude.ToString(),
                    ["hits"] = selectedTargets.Count.ToString(),
                }
            );
            return;
        }

        var target = RiftboundEffectUnitTargeting.ResolveTargetUnitFromAction(session, actionId)
            ?? selectedTargets.FirstOrDefault();
        if (target is null)
        {
            return;
        }

        var hitCount = Math.Max(1, runtime.ReadIntEffectData(card, "hitCount", fallback: 2));
        for (var i = 0; i < hitCount; i += 1)
        {
            target.MarkedDamage += magnitude;
        }

        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "Resolve",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["target"] = target.Name,
                ["magnitude"] = magnitude.ToString(),
                ["hits"] = hitCount.ToString(),
            }
        );
    }
}
