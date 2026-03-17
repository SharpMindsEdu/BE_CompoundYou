using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class CardSharpEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "card-sharp";
    public override string TemplateId => "named.card-sharp";

    public override void OnUnitPlay(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        string actionId
    )
    {
        var opponentsWhoPlayed = 0;
        foreach (var opponent in session.Players.Where(x => x.PlayerIndex != player.PlayerIndex))
        {
            opponent.BaseZone.Cards.Add(
                RiftboundTokenFactory.CreateGoldGearToken(
                    ownerPlayerIndex: opponent.PlayerIndex,
                    controllerPlayerIndex: opponent.PlayerIndex,
                    exhausted: true
                )
            );
            opponentsWhoPlayed += 1;
        }

        player.BaseZone.Cards.Add(
            RiftboundTokenFactory.CreateGoldGearToken(
                ownerPlayerIndex: player.PlayerIndex,
                controllerPlayerIndex: player.PlayerIndex,
                exhausted: true
            )
        );

        for (var i = 0; i < opponentsWhoPlayed; i += 1)
        {
            player.BaseZone.Cards.Add(
                RiftboundTokenFactory.CreateGoldGearToken(
                    ownerPlayerIndex: player.PlayerIndex,
                    controllerPlayerIndex: player.PlayerIndex,
                    exhausted: true
                )
            );
        }

        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "WhenPlay",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["opponentsWhoPlayed"] = opponentsWhoPlayed.ToString(),
            }
        );
    }
}
