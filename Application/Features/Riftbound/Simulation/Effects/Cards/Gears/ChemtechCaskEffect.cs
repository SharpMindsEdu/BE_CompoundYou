using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class ChemtechCaskEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "chemtech-cask";
    public override string TemplateId => "named.chemtech-cask";

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
            card.IsExhausted
            || session.TurnPlayerIndex == player.PlayerIndex
            || playedCard.ControllerPlayerIndex != player.PlayerIndex
            || !string.Equals(playedCard.Type, "Spell", StringComparison.OrdinalIgnoreCase)
        )
        {
            return;
        }

        card.IsExhausted = true;
        player.BaseZone.Cards.Add(
            RiftboundTokenFactory.CreateGoldGearToken(
                ownerPlayerIndex: player.PlayerIndex,
                controllerPlayerIndex: player.PlayerIndex,
                exhausted: true
            )
        );
    }
}

