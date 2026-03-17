using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class ScrapheapEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "scrapheap";
    public override string TemplateId => "named.scrapheap";

    public override void OnSpellOrGearPlay(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        string actionId
    )
    {
        DrawOne(runtime, session, player, card, "Resolve");
    }

    public override void OnDiscardFromHand(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        CardInstance? sourceCard,
        string reason
    )
    {
        DrawOne(runtime, session, player, card, "WhenDiscard");
    }

    private static void DrawOne(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        string timing
    )
    {
        runtime.DrawCards(player, 1);
        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            timing,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["drawn"] = "1",
            }
        );
    }
}
