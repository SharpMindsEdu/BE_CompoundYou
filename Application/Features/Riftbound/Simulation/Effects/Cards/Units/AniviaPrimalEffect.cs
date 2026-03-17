using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class AniviaPrimalEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "anivia-primal";
    public override string TemplateId => "named.anivia-primal";

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

        var damage = 3 + runtime.GetSpellAndAbilityBonusDamage(session, player.PlayerIndex);
        var targets = battlefield.Units
            .Where(x => x.ControllerPlayerIndex != player.PlayerIndex)
            .ToList();
        if (targets.Count == 0)
        {
            return;
        }

        foreach (var target in targets)
        {
            target.MarkedDamage += damage;
        }

        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "WhenAttack",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["targets"] = targets.Count.ToString(),
                ["damagePerTarget"] = damage.ToString(),
                ["battlefield"] = battlefield.Name,
            }
        );
    }
}
