using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class GroveOfTheGodWillowEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "grove-of-the-god-willow";
    public override string TemplateId => "named.grove-of-the-god-willow";

    public override void OnHoldScore(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        BattlefieldState battlefield
    )
    {
        runtime.DrawCards(player, 1);
        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "WhenHold",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["battlefield"] = battlefield.Name,
                ["draw"] = "1",
            }
        );
    }
}

