using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class ThermoBeamEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "thermo-beam";
    public override string TemplateId => "named.thermo-beam";

    public override void OnSpellOrGearPlay(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        string actionId
    )
    {
        var killed = 0;
        var allGear = RiftboundEffectGearTargeting.EnumerateAllGear(session).ToList();
        foreach (var gear in allGear)
        {
            if (!RiftboundEffectGearTargeting.RemoveGearFromBoard(session, gear))
            {
                continue;
            }

            var owner = RiftboundEffectGearTargeting.ResolveOwnerPlayer(session, gear);
            owner.TrashZone.Cards.Add(gear);
            killed += 1;

            if (
                string.Equals(gear.Name, "Scrapheap", StringComparison.OrdinalIgnoreCase)
                || string.Equals(gear.EffectTemplateId, "named.scrapheap", StringComparison.Ordinal)
            )
            {
                runtime.DrawCards(owner, 1);
            }
        }

        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "Resolve",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["killedGear"] = killed.ToString(),
            }
        );
    }
}

