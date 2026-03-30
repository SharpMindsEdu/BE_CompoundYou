using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class LuxLadyOfLuminosityEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "lux-lady-of-luminosity";
    public override string TemplateId => "named.lux-lady-of-luminosity";

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
            playedCard.ControllerPlayerIndex != player.PlayerIndex
            || !string.Equals(playedCard.Type, "Spell", StringComparison.OrdinalIgnoreCase)
            || playedCard.Cost.GetValueOrDefault() < 5
        )
        {
            return;
        }

        runtime.DrawCards(player, 1);
        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "WhenPlaySpell",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["spell"] = playedCard.Name,
                ["draw"] = "1",
            }
        );
    }
}
