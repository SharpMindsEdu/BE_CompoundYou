using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class EmberMonkEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "ember-monk";
    public override string TemplateId => "named.ember-monk";

    public override void OnFriendlyCardPlayed(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        CardInstance playedCard,
        string actionId
    )
    {
        if (!playedCard.Keywords.Contains("Hidden", StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        card.TemporaryMightModifier += 2;
    }
}

