using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class CarnivorousSnapvineEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "carnivorous-snapvine";
    public override string TemplateId => "named.carnivorous-snapvine";

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
                .Select(x => ($"-to-bf-{x.Index}", $"Play {card.Name} to battlefield {x.Name}"))
        );

        var enemyBattlefieldUnits = session.Battlefields
            .SelectMany(x => x.Units)
            .Where(x => x.ControllerPlayerIndex != player.PlayerIndex)
            .ToList();
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

            foreach (var enemy in enemyBattlefieldUnits)
            {
                actions.Add(
                    new RiftboundLegalAction(
                        $"{runtime.ActionPrefix}play-{card.InstanceId}-target-unit-{enemy.InstanceId}{destination.Suffix}",
                        RiftboundActionType.PlayCard,
                        player.PlayerIndex,
                        $"{destination.Description} and challenge {enemy.Name}"
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
        var target = RiftboundEffectUnitTargeting.ResolveTargetUnitFromAction(session, actionId);
        if (
            target is null
            || target.ControllerPlayerIndex == player.PlayerIndex
            || RiftboundEffectUnitTargeting.FindBattlefieldContainingUnit(session, target.InstanceId) is null
        )
        {
            return;
        }

        var selfDamage = runtime.GetEffectiveMight(session, target);
        var targetDamage = runtime.GetEffectiveMight(session, card);
        card.MarkedDamage += selfDamage;
        target.MarkedDamage += targetDamage;

        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "WhenPlay",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["target"] = target.Name,
                ["selfDamage"] = selfDamage.ToString(),
                ["targetDamage"] = targetDamage.ToString(),
            }
        );
    }
}
