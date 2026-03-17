using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class ArenaBarEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "arena-bar";
    public override string TemplateId => "named.arena-bar";
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

        var target = RiftboundEffectUnitTargeting.EnumerateFriendlyUnits(session, player.PlayerIndex)
            .Where(x => x.IsExhausted)
            .OrderByDescending(x => x.Might.GetValueOrDefault() + x.PermanentMightModifier + x.TemporaryMightModifier)
            .ThenBy(x => x.Name, StringComparer.Ordinal)
            .ThenBy(x => x.InstanceId)
            .FirstOrDefault();
        if (target is null)
        {
            return false;
        }

        card.IsExhausted = true;
        var gainedBuff = false;
        if (target.PermanentMightModifier <= 0)
        {
            target.PermanentMightModifier += 1;
            gainedBuff = true;
        }

        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "Activate",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["target"] = target.Name,
                ["gainedBuff"] = gainedBuff ? "true" : "false",
            }
        );
        return true;
    }
}
