using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class LeeSinAsceticEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "lee-sin-ascetic";
    public override string TemplateId => "named.lee-sin-ascetic";
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

        card.IsExhausted = true;
        card.PermanentMightModifier += 1;
        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "Activate",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["buffedSelf"] = "true",
            }
        );
        return true;
    }

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
        if (!isDefender)
        {
            return;
        }

        card.TemporaryMightModifier += 1;
    }
}
