using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class BlackMarketBrokerEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "black-market-broker";
    public override string TemplateId => "named.black-market-broker";

    public override void OnFriendlyCardPlayed(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        CardInstance playedCard,
        string actionId
    )
    {
        if (playedCard.ControllerPlayerIndex != player.PlayerIndex || !playedCard.IsFacedown)
        {
            return;
        }

        player.BaseZone.Cards.Add(
            RiftboundTokenFactory.CreateGoldGearToken(
                ownerPlayerIndex: player.PlayerIndex,
                controllerPlayerIndex: player.PlayerIndex,
                exhausted: true
            )
        );

        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "WhenPlayFromFaceDown",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["playedCard"] = playedCard.Name,
                ["playedGoldToken"] = "true",
            }
        );
    }
}

