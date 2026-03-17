using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class DunebreakerEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "dunebreaker";
    public override string TemplateId => "named.dunebreaker";

    public override void OnUnitPlay(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        string actionId
    )
    {
        if (player.HandZone.Cards.Count <= 2)
        {
            card.IsExhausted = false;
        }
    }

    public override void OnHoldScore(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        BattlefieldState battlefield
    )
    {
        runtime.DrawCards(player, 2);
    }
}

