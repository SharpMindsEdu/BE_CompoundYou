using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class GarbageGrabberEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "garbage-grabber";
    public override string TemplateId => "named.garbage-grabber";
    public override bool HasActivatedAbility => true;

    public override bool TryActivateAbility(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card
    )
    {
        if (card.IsExhausted)
        {
            return false;
        }

        if (!runtime.TryPayCost(session, player, energyCost: 1))
        {
            return false;
        }

        card.IsExhausted = true;
        var recycled = 0;
        var recycleTargets = player.TrashZone.Cards
            .OrderBy(x => x.Name, StringComparer.Ordinal)
            .ThenBy(x => x.InstanceId)
            .Take(3)
            .ToList();
        foreach (var target in recycleTargets)
        {
            if (!player.TrashZone.Cards.Remove(target))
            {
                continue;
            }

            player.MainDeckZone.Cards.Add(target);
            recycled += 1;
        }

        runtime.DrawCards(player, 1);
        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "Activate",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["recycled"] = recycled.ToString(),
                ["draw"] = "1",
            }
        );
        return true;
    }
}
