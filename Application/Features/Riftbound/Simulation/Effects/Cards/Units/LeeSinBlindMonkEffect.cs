using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class LeeSinBlindMonkEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "lee-sin-blind-monk";
    public override string TemplateId => "named.lee-sin-blind-monk";
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
            .OrderByDescending(x => runtime.GetEffectiveMight(session, x))
            .ThenBy(x => x.Name, StringComparer.Ordinal)
            .ThenBy(x => x.InstanceId)
            .FirstOrDefault();
        if (target is null)
        {
            return false;
        }

        if (!runtime.TryPayCost(session, player, energyCost: 1))
        {
            return false;
        }

        card.IsExhausted = true;
        if (target.PermanentMightModifier <= 0)
        {
            target.PermanentMightModifier += 1;
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
                ["buffed"] = (target.PermanentMightModifier > 0).ToString().ToLowerInvariant(),
            }
        );
        return true;
    }
}
