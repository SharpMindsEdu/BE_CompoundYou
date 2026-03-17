using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class CemeteryAttendantEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "cemetery-attendant";
    public override string TemplateId => "named.cemetery-attendant";

    public override void OnUnitPlay(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        string actionId
    )
    {
        var target = player.TrashZone.Cards
            .Where(x => string.Equals(x.Type, "Unit", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.Might.GetValueOrDefault())
            .ThenByDescending(x => x.Cost.GetValueOrDefault())
            .ThenBy(x => x.Name, StringComparer.Ordinal)
            .ThenBy(x => x.InstanceId)
            .FirstOrDefault();
        if (target is null)
        {
            return;
        }

        player.TrashZone.Cards.Remove(target);
        player.HandZone.Cards.Add(target);
        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "WhenPlay",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["returnedUnit"] = target.Name,
            }
        );
    }
}
