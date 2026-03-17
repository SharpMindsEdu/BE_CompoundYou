using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class BloodRushEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "blood-rush";
    public override string TemplateId => "named.blood-rush";

    protected override IReadOnlyDictionary<string, string> BuildData(
        RiftboundCard card,
        string normalizedEffectText
    )
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["temporaryAssaultBonus"] = "2",
            ["repeatEnergyCost"] = "1",
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
        foreach (var target in RiftboundEffectUnitTargeting.EnumerateAllUnits(session))
        {
            var actionId = $"{runtime.ActionPrefix}play-{card.InstanceId}-spell-target-unit-{target.InstanceId}";
            actions.Add(
                new RiftboundLegalAction(
                    actionId,
                    RiftboundActionType.PlayCard,
                    player.PlayerIndex,
                    $"Play {card.Name} targeting {target.Name}"
                )
            );
            actions.Add(
                new RiftboundLegalAction(
                    $"{actionId}{runtime.RepeatActionSuffix}",
                    RiftboundActionType.PlayCard,
                    player.PlayerIndex,
                    $"Play {card.Name} targeting {target.Name} (repeat)"
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
        var target = RiftboundEffectUnitTargeting.ResolveTargetUnitFromAction(session, actionId);
        if (target is null)
        {
            return;
        }

        ApplyAssaultBonus(runtime, session, player, card, target, repeated: false);
        if (runtime.IsRepeatRequested(actionId) && runtime.TryPayRepeatCost(session, player, card))
        {
            ApplyAssaultBonus(runtime, session, player, card, target, repeated: true);
        }
    }

    private static void ApplyAssaultBonus(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance source,
        CardInstance target,
        bool repeated
    )
    {
        var current = runtime.ReadIntEffectData(target, "temporaryAssaultBonus", fallback: 0);
        target.EffectData["temporaryAssaultBonus"] = (current + 2).ToString();
        runtime.AddEffectContext(
            session,
            source.Name,
            player.PlayerIndex,
            "Resolve",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = source.EffectTemplateId,
                ["target"] = target.Name,
                ["assaultBonus"] = "2",
                ["repeat"] = repeated ? "true" : "false",
            }
        );
    }
}

