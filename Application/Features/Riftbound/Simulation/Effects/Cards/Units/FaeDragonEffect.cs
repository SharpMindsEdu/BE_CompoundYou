using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class FaeDragonEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "fae-dragon";
    public override string TemplateId => "named.fae-dragon";

    public override void OnUnitPlay(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        string actionId
    )
    {
        var candidates = RiftboundEffectUnitTargeting.EnumerateFriendlyUnits(session, player.PlayerIndex)
            .Where(x => x.PermanentMightModifier <= 0)
            .OrderBy(x => x.Name, StringComparer.Ordinal)
            .ThenBy(x => x.InstanceId)
            .Take(4)
            .ToList();
        foreach (var candidate in candidates)
        {
            candidate.PermanentMightModifier += 1;
        }
    }
}

