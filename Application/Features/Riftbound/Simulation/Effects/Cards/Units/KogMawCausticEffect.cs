using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class KogMawCausticEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "kog-maw-caustic";
    public override string TemplateId => "named.kog-maw-caustic";

    public override void OnDeath(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card
    )
    {
        var battlefield = RiftboundEffectUnitTargeting.FindBattlefieldContainingUnit(
            session,
            card.InstanceId
        );
        if (battlefield is null)
        {
            return;
        }

        var magnitude = 4 + runtime.GetSpellAndAbilityBonusDamage(session, player.PlayerIndex);
        var targets = battlefield.Units.ToList();
        foreach (var target in targets)
        {
            target.MarkedDamage += magnitude;
        }

        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "Deathknell",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["battlefield"] = battlefield.Name,
                ["targets"] = targets.Count.ToString(),
                ["magnitude"] = magnitude.ToString(),
            }
        );
    }
}
