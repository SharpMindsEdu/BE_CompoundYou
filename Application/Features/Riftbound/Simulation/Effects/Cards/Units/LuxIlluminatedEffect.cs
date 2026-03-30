using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class LuxIlluminatedEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "lux-illuminated";
    public override string TemplateId => "named.lux-illuminated";

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

        card.TemporaryMightModifier += 3;
        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "WhenPlaySpell",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["spell"] = playedCard.Name,
                ["magnitude"] = "3",
            }
        );
    }
}
