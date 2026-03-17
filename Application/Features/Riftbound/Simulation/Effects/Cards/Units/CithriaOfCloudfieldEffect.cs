using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class CithriaOfCloudfieldEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "cithria-of-cloudfield";
    public override string TemplateId => "named.cithria-of-cloudfield";

    public override void OnFriendlyCardPlayed(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        CardInstance playedCard,
        string actionId
    )
    {
        if (
            playedCard.InstanceId == card.InstanceId
            || playedCard.ControllerPlayerIndex != player.PlayerIndex
            || !string.Equals(playedCard.Type, "Unit", StringComparison.OrdinalIgnoreCase)
            || card.PermanentMightModifier > 0
        )
        {
            return;
        }

        card.PermanentMightModifier += 1;
    }
}

