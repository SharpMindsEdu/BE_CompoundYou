using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class BondsOfStrengthEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "bonds-of-strength";
    public override string TemplateId => "named.bonds-of-strength";

    protected override IReadOnlyDictionary<string, string> BuildData(
        RiftboundCard card,
        string normalizedEffectText
    )
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["repeatEnergyCost"] = "2",
            ["magnitude"] = "1",
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
        var units = RiftboundEffectUnitTargeting.EnumerateFriendlyUnits(session, player.PlayerIndex).ToList();
        for (var left = 0; left < units.Count; left += 1)
        {
            for (var right = left + 1; right < units.Count; right += 1)
            {
                var targetList = $"{units[left].InstanceId},{units[right].InstanceId}";
                var actionId = $"{runtime.ActionPrefix}play-{card.InstanceId}-spell{runtime.MultiTargetUnitsMarker}{targetList}";
                actions.Add(
                    new RiftboundLegalAction(
                        actionId,
                        RiftboundActionType.PlayCard,
                        player.PlayerIndex,
                        $"Play {card.Name} targeting {units[left].Name} and {units[right].Name}"
                    )
                );
                actions.Add(
                    new RiftboundLegalAction(
                        $"{actionId}{runtime.RepeatActionSuffix}",
                        RiftboundActionType.PlayCard,
                        player.PlayerIndex,
                        $"Play {card.Name} targeting {units[left].Name} and {units[right].Name} (repeat)"
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
        var selected = RiftboundEffectUnitTargeting.ResolveTargetUnitsFromAction(session, actionId)
            .Where(x => x.ControllerPlayerIndex == player.PlayerIndex)
            .Take(2)
            .ToList();
        if (selected.Count != 2)
        {
            return;
        }

        ApplyBuff(runtime, session, player, card, selected, repeated: false);
        if (runtime.IsRepeatRequested(actionId) && runtime.TryPayRepeatCost(session, player, card))
        {
            ApplyBuff(runtime, session, player, card, selected, repeated: true);
        }
    }

    private static void ApplyBuff(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance source,
        IReadOnlyCollection<CardInstance> targets,
        bool repeated
    )
    {
        foreach (var target in targets)
        {
            target.TemporaryMightModifier += 1;
        }

        runtime.AddEffectContext(
            session,
            source.Name,
            player.PlayerIndex,
            "Resolve",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = source.EffectTemplateId,
                ["targets"] = targets.Count.ToString(),
                ["magnitude"] = "1",
                ["repeat"] = repeated ? "true" : "false",
            }
        );
    }
}

