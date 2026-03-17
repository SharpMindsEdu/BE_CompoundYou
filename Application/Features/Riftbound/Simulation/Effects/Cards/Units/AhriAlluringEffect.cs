using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class AhriAlluringEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "ahri-alluring";
    public override string TemplateId => "named.ahri-alluring";

    public override void OnHoldScore(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        BattlefieldState battlefield
    )
    {
        player.Score += 1;
        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "WhenHold",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["bonusScore"] = "1",
                ["battlefield"] = battlefield.Name,
            }
        );
    }
}
