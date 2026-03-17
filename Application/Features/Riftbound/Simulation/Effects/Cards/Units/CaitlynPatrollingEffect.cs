using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class CaitlynPatrollingEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "caitlyn-patrolling";
    public override string TemplateId => "named.caitlyn-patrolling";
    public override bool HasActivatedAbility => true;

    public override bool TryActivateAbility(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card
    )
    {
        if (card.IsExhausted)
        {
            return false;
        }

        var ownBattlefield = RiftboundEffectUnitTargeting.FindBattlefieldContainingUnit(
            session,
            card.InstanceId
        );
        if (ownBattlefield is null)
        {
            return false;
        }

        var target = session.Battlefields
            .SelectMany(x => x.Units)
            .Where(x => x.InstanceId != card.InstanceId)
            .OrderByDescending(x => x.ControllerPlayerIndex != player.PlayerIndex)
            .ThenByDescending(x => runtime.GetEffectiveMight(session, x))
            .ThenBy(x => x.Name, StringComparer.Ordinal)
            .ThenBy(x => x.InstanceId)
            .FirstOrDefault();
        if (target is null)
        {
            return false;
        }

        card.IsExhausted = true;
        var damage =
            runtime.GetEffectiveMight(session, card)
            + runtime.GetSpellAndAbilityBonusDamage(session, player.PlayerIndex);
        target.MarkedDamage += damage;
        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "Activate",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["target"] = target.Name,
                ["damage"] = damage.ToString(),
            }
        );
        return true;
    }
}
