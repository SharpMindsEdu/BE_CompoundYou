using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class LeonaDeterminedEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "leona-determined";
    public override string TemplateId => "named.leona-determined";

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
        if (isDefender)
        {
            card.TemporaryMightModifier += 1;
        }

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

        target.IsExhausted = true;
        target.EffectData["stunnedThisTurn"] = "true";
        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "WhenAttack",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["stunned"] = target.Name,
                ["battlefield"] = battlefield.Name,
            }
        );
    }
}
