using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class GlascMixologistEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "glasc-mixologist";
    public override string TemplateId => "named.glasc-mixologist";

    public override void OnDeath(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card
    )
    {
        var candidate = player.TrashZone.Cards
            .Where(x =>
                string.Equals(x.Type, "Unit", StringComparison.OrdinalIgnoreCase)
                && x.Cost.GetValueOrDefault() <= 3
                && x.Power.GetValueOrDefault() <= 1
            )
            .OrderByDescending(x => x.Might.GetValueOrDefault())
            .ThenByDescending(x => x.Cost.GetValueOrDefault())
            .ThenBy(x => x.Name, StringComparer.Ordinal)
            .ThenBy(x => x.InstanceId)
            .FirstOrDefault();
        if (candidate is null)
        {
            return;
        }

        player.TrashZone.Cards.Remove(candidate);
        candidate.IsExhausted = true;
        player.BaseZone.Cards.Add(candidate);
        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "Deathknell",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["playedFromTrash"] = candidate.Name,
                ["maxCost"] = "3",
                ["maxPower"] = "1",
            }
        );
    }
}

