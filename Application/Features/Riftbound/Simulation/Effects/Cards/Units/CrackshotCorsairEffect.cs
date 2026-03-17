using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class CrackshotCorsairEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "crackshot-corsair";
    public override string TemplateId => "named.crackshot-corsair";

    public override void OnShowdownStart(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        BattlefieldState battlefield,
        bool isAttacker,
        bool isDefender
    )
    {
        if (!isAttacker)
        {
            return;
        }

        var target = battlefield.Units
            .Where(x => x.ControllerPlayerIndex != player.PlayerIndex)
            .OrderByDescending(x => runtime.GetEffectiveMight(session, x))
            .ThenBy(x => x.Name, StringComparer.Ordinal)
            .ThenBy(x => x.InstanceId)
            .FirstOrDefault();
        if (target is null)
        {
            return;
        }

        target.MarkedDamage += 1 + runtime.GetSpellAndAbilityBonusDamage(session, player.PlayerIndex);
    }
}

