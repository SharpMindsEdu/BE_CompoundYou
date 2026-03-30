using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class JinxLooseCannonEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "jinx-loose-cannon";
    public override string TemplateId => "named.jinx-loose-cannon";

    public override void OnTurnBeginning(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card
    )
    {
        var handCountBefore = player.HandZone.Cards.Count;
        if (handCountBefore > 1)
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
                ["handCountBefore"] = handCountBefore.ToString(),
            }
        );
    }
}
