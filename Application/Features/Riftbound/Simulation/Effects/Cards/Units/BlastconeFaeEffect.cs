using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class BlastconeFaeEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "blastcone-fae";
    public override string TemplateId => "named.blastcone-fae";

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

        var units = RiftboundEffectUnitTargeting.EnumerateAllUnits(session).ToList();
        if (units.Count == 0)
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
            foreach (var target in units)
            {
                actions.Add(
                    new RiftboundLegalAction(
                        $"{runtime.ActionPrefix}play-{card.InstanceId}-target-unit-{target.InstanceId}{destination.Suffix}",
                        RiftboundActionType.PlayCard,
                        player.PlayerIndex,
                        $"{destination.Description} targeting {target.Name}"
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
        var targetUnit = RiftboundEffectUnitTargeting.ResolveTargetUnitFromAction(session, actionId);
        if (targetUnit is null)
        {
            return;
        }

        var currentMight =
            targetUnit.Might.GetValueOrDefault()
            + targetUnit.PermanentMightModifier
            + targetUnit.TemporaryMightModifier;
        var maxReduction = Math.Max(0, currentMight - 1);
        var reduction = Math.Min(2, maxReduction);
        if (reduction <= 0)
        {
            return;
        }

        targetUnit.TemporaryMightModifier -= reduction;
        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "WhenPlay",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["target"] = targetUnit.Name,
                ["magnitude"] = reduction.ToString(),
            }
        );
    }
}

