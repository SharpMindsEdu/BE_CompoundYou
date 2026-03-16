using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class BackToBackEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "back-to-back";
    public override string TemplateId => "named.back-to-back";

    protected override IReadOnlyDictionary<string, string> BuildData(
        RiftboundCard card,
        string normalizedEffectText
    )
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["magnitude"] = (RiftboundEffectTextParser.TryExtractMagnitude(normalizedEffectText) ?? 2).ToString(),
            ["targetCount"] = "2",
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
        var targetCount = Math.Max(2, runtime.ReadIntEffectData(card, "targetCount", fallback: 2));
        var friendlyUnits = RiftboundEffectUnitTargeting.EnumerateFriendlyUnits(
            session,
            player.PlayerIndex
        );
        if (friendlyUnits.Count < targetCount)
        {
            return true;
        }

        var friendlyList = friendlyUnits.ToList();
        foreach (var pair in EnumerateCombinations(friendlyList, targetCount))
        {
            var targetIds = string.Join(",", pair.Select(x => x.InstanceId.ToString()));
            var actionId =
                $"{runtime.ActionPrefix}play-{card.InstanceId}-spell{runtime.MultiTargetUnitsMarker}{targetIds}";
            actions.Add(
                new RiftboundLegalAction(
                    actionId,
                    RiftboundActionType.PlayCard,
                    player.PlayerIndex,
                    $"Play {card.Name} targeting {targetCount} friendly unit(s)"
                )
            );
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
        var targetCount = Math.Max(2, runtime.ReadIntEffectData(card, "targetCount", fallback: 2));
        var selectedTargets = RiftboundEffectUnitTargeting.ResolveTargetUnitsFromAction(session, actionId)
            .Where(x => x.ControllerPlayerIndex == player.PlayerIndex)
            .DistinctBy(x => x.InstanceId)
            .ToList();
        if (selectedTargets.Count != targetCount)
        {
            return;
        }

        var magnitude = runtime.ReadMagnitude(card, fallback: 2);
        foreach (var target in selectedTargets)
        {
            target.TemporaryMightModifier += magnitude;
        }

        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "Resolve",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["targets"] = selectedTargets.Count.ToString(),
                ["magnitude"] = magnitude.ToString(),
                ["targetNames"] = string.Join(", ", selectedTargets.Select(x => x.Name)),
            }
        );
    }

    private static IEnumerable<IReadOnlyList<CardInstance>> EnumerateCombinations(
        IReadOnlyList<CardInstance> source,
        int count
    )
    {
        if (count <= 0 || source.Count < count)
        {
            yield break;
        }

        var indices = Enumerable.Range(0, count).ToArray();
        while (true)
        {
            var current = new CardInstance[count];
            for (var i = 0; i < count; i += 1)
            {
                current[i] = source[indices[i]];
            }

            yield return current;

            var step = count - 1;
            while (step >= 0 && indices[step] == source.Count - count + step)
            {
                step -= 1;
            }

            if (step < 0)
            {
                yield break;
            }

            indices[step] += 1;
            for (var j = step + 1; j < count; j += 1)
            {
                indices[j] = indices[j - 1] + 1;
            }
        }
    }
}
