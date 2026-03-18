using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class FlameChompersEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "flame-chompers";
    public override string TemplateId => "named.flame-chompers";

    public override void OnDiscardFromHand(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        CardInstance? sourceCard,
        string reason
    )
    {
        if (!player.TrashZone.Cards.Any(x => x.InstanceId == card.InstanceId))
        {
            return;
        }

        if (
            !runtime.TryPayCost(
                session,
                player,
                energyCost: 0,
                [new EffectPowerRequirement(1, ["Fury"])]
            )
        )
        {
            return;
        }

        player.TrashZone.Cards.RemoveAll(x => x.InstanceId == card.InstanceId);
        card.IsExhausted = true;
        player.BaseZone.Cards.Add(card);
        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "WhenDiscarded",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["paidFury"] = "true",
                ["reason"] = reason,
            }
        );
    }
}
