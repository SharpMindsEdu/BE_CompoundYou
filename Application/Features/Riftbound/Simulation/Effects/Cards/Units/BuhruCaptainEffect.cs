using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class BuhruCaptainEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "buhru-captain";
    public override string TemplateId => "named.buhru-captain";

    public override void OnUnitPlay(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        string actionId
    )
    {
        var shouldDraw = player.MainDeckZone.Cards.Count > 0;
        if (shouldDraw)
        {
            runtime.DrawCards(player, 1);
        }
        else
        {
            card.PermanentMightModifier += 1;
        }

        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "WhenPlay",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["choice"] = shouldDraw ? "draw" : "buff",
            }
        );
    }
}
