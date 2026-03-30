using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class JinxRebelEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "jinx-rebel";
    public override string TemplateId => "named.jinx-rebel";

    public override void OnFriendlyCardDiscarded(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        CardInstance discardedCard,
        CardInstance? sourceCard,
        string reason
    )
    {
        if (card.ControllerPlayerIndex != player.PlayerIndex)
        {
            return;
        }

        card.IsExhausted = false;
        card.TemporaryMightModifier += 1;
        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "WhenDiscard",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["discarded"] = discardedCard.Name,
                ["reason"] = reason,
                ["sourceCard"] = sourceCard?.Name ?? string.Empty,
                ["ready"] = "true",
                ["tempMight"] = "1",
            }
        );
    }
}
