using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class FightOrFlightEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "fight-or-flight";
    public override string TemplateId => "named.fight-or-flight";

    public override bool TryAddLegalActions(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        List<RiftboundLegalAction> actions
    )
    {
        foreach (var target in session.Battlefields.SelectMany(x => x.Units).OrderBy(x => x.Name, StringComparer.Ordinal).ThenBy(x => x.InstanceId))
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
        if (target is null)
        {
            return;
        }

        var battlefield = RiftboundEffectUnitTargeting.FindBattlefieldContainingUnit(session, target.InstanceId);
        if (battlefield is null)
        {
            return;
        }

        battlefield.Units.Remove(target);
        var owner = session.Players.FirstOrDefault(x => x.PlayerIndex == target.OwnerPlayerIndex);
        owner?.BaseZone.Cards.Add(target);

        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "Resolve",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["target"] = target.Name,
                ["to"] = owner is null ? "base-unknown" : $"base-{owner.PlayerIndex}",
            }
        );
    }
}
