using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class BlastCorpsCadetEffect : RiftboundNamedCardEffectBase
{
    private const string AdditionalCostActionMarker = "-blast-corps-additional-cost-";

    public override string NameIdentifier => "blast-corps-cadet";
    public override string TemplateId => "named.blast-corps-cadet";

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

        var battlefieldUnits = session.Battlefields.SelectMany(x => x.Units).ToList();
        foreach (var destination in destinations)
        {
            foreach (var target in battlefieldUnits)
            {
                actions.Add(
                    new RiftboundLegalAction(
                        $"{runtime.ActionPrefix}play-{card.InstanceId}{AdditionalCostActionMarker}target-unit-{target.InstanceId}{destination.Suffix}",
                        RiftboundActionType.PlayCard,
                        player.PlayerIndex,
                        $"{destination.Description} (+[1][Fury], deal 2 to {target.Name})"
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
        if (!actionId.Contains(AdditionalCostActionMarker, StringComparison.Ordinal))
        {
            return;
        }

        var targetUnit = RiftboundEffectUnitTargeting.ResolveTargetUnitFromAction(session, actionId);
        if (
            targetUnit is null
            || RiftboundEffectUnitTargeting.FindBattlefieldContainingUnit(session, targetUnit.InstanceId)
                is null
        )
        {
            return;
        }

        var magnitude = 2 + runtime.GetSpellAndAbilityBonusDamage(session, player.PlayerIndex);
        targetUnit.MarkedDamage += magnitude;
        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "WhenPlay",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["paidAdditionalCost"] = "true",
                ["target"] = targetUnit.Name,
                ["magnitude"] = magnitude.ToString(),
            }
        );
    }
}

