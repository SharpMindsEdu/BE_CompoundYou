using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class AhriNineTailedFoxEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "ahri-nine-tailed-fox";
    public override string TemplateId => "named.ahri-nine-tailed-fox";

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
        if (battlefield.ControlledByPlayerIndex != player.PlayerIndex)
        {
            return;
        }

        var attackerPlayerIndex = session.Combat.AttackerPlayerIndex;
        if (!attackerPlayerIndex.HasValue || attackerPlayerIndex.Value == player.PlayerIndex)
        {
            return;
        }

        var affected = 0;
        foreach (var attacker in battlefield.Units.Where(x => x.ControllerPlayerIndex == attackerPlayerIndex.Value))
        {
            var currentMight = attacker.Might.GetValueOrDefault()
                + attacker.PermanentMightModifier
                + attacker.TemporaryMightModifier;
            if (currentMight <= 1)
            {
                continue;
            }

            attacker.TemporaryMightModifier -= 1;
            affected += 1;
        }

        if (affected <= 0)
        {
            return;
        }

        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "WhenEnemyAttacksControlledBattlefield",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["battlefield"] = battlefield.Name,
                ["affected"] = affected.ToString(),
            }
        );
    }
}
