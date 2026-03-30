using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class MushroomPouchEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "mushroom-pouch";
    public override string TemplateId => "named.mushroom-pouch";

    public override void OnTurnBeginning(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card
    )
    {
        var hasFacedownAtBattlefield = session.Battlefields.Any(x =>
            x.HiddenCards.Any(hidden => hidden.ControllerPlayerIndex == player.PlayerIndex)
        );
        if (!hasFacedownAtBattlefield)
        {
            return;
        }

        runtime.DrawCards(player, 1);
        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "Beginning",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["drawn"] = "1",
            }
        );
    }
}
