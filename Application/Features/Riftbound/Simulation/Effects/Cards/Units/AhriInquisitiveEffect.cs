using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class AhriInquisitiveEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "ahri-inquisitive";
    public override string TemplateId => "named.ahri-inquisitive";

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
        if (!isAttacker && !isDefender)
        {
            return;
        }

        var target = battlefield.Units
            .Where(x => x.ControllerPlayerIndex != player.PlayerIndex)
            .OrderByDescending(x => x.Might.GetValueOrDefault() + x.PermanentMightModifier + x.TemporaryMightModifier)
            .ThenBy(x => x.Name, StringComparer.Ordinal)
            .ThenBy(x => x.InstanceId)
            .FirstOrDefault();
        if (target is null)
        {
            return;
        }

        var currentMight = target.Might.GetValueOrDefault()
            + target.PermanentMightModifier
            + target.TemporaryMightModifier;
        var reduction = Math.Min(2, Math.Max(0, currentMight - 1));
        if (reduction <= 0)
        {
            return;
        }

        target.TemporaryMightModifier -= reduction;
        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "WhenAttackOrDefend",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["target"] = target.Name,
                ["reduction"] = reduction.ToString(),
                ["battlefield"] = battlefield.Name,
            }
        );
    }
}
