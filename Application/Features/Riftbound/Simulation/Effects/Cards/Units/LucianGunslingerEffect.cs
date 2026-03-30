using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class LucianGunslingerEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "lucian-gunslinger";
    public override string TemplateId => "named.lucian-gunslinger";

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

        card.TemporaryMightModifier += 1;
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

        target.MarkedDamage += 1;
        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "WhenAttack",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["target"] = target.Name,
                ["damage"] = "1",
            }
        );
    }
}
