using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class FlurryOfBladesEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "flurry-of-blades";
    public override string TemplateId => "named.flurry-of-blades";

    public override void OnSpellOrGearPlay(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        string actionId
    )
    {
        var magnitude = runtime.ReadMagnitude(card, fallback: 1)
            + runtime.GetSpellAndAbilityBonusDamage(session, player.PlayerIndex);
        foreach (var unit in session.Battlefields.SelectMany(x => x.Units))
        {
            unit.MarkedDamage += magnitude;
        }
    }
}
