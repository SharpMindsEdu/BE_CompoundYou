using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class ExperimentalHexplateEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "experimental-hexplate";
    public override string TemplateId => "named.experimental-hexplate";

    protected override IReadOnlyCollection<string> BuildKeywords(
        RiftboundCard card,
        string normalizedEffectText,
        IReadOnlySet<string> baseKeywords
    )
    {
        var keywords = baseKeywords.ToList();
        if (!keywords.Contains("Mech", StringComparer.OrdinalIgnoreCase))
        {
            keywords.Add("Mech");
        }

        return keywords
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    protected override IReadOnlyDictionary<string, string> BuildData(
        RiftboundCard card,
        string normalizedEffectText
    )
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["attachedMightBonus"] = "1",
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
        foreach (var target in RiftboundEffectUnitTargeting.EnumerateFriendlyUnits(session, player.PlayerIndex))
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
        if (target is null || target.ControllerPlayerIndex != player.PlayerIndex)
        {
            return;
        }

        card.AttachedToInstanceId = target.InstanceId;
        card.IsExhausted = true;
        var battlefield = RiftboundEffectUnitTargeting.FindBattlefieldContainingUnit(session, target.InstanceId);
        if (battlefield is not null)
        {
            battlefield.Gear.Add(card);
        }
        else
        {
            player.BaseZone.Cards.Add(card);
        }

        runtime.NotifyGearAttached(session, card, target);
    }
}

