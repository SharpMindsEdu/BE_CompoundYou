using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class BlastOfPowerEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "blast-of-power";
    public override string TemplateId => "named.blast-of-power";

    public override bool TryAddLegalActions(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        List<RiftboundLegalAction> actions
    )
    {
        foreach (var target in session.Battlefields.SelectMany(x => x.Units))
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
        var targetUnit = RiftboundEffectUnitTargeting.ResolveTargetUnitFromAction(session, actionId);
        if (
            targetUnit is null
            || RiftboundEffectUnitTargeting.FindBattlefieldContainingUnit(session, targetUnit.InstanceId)
                is null
        )
        {
            return;
        }

        targetUnit.MarkedDamage = runtime.GetEffectiveMight(session, targetUnit);
        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "Resolve",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["target"] = targetUnit.Name,
            }
        );
    }
}

