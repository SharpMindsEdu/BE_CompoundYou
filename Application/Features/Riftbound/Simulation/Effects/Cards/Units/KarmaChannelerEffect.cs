using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class KarmaChannelerEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "karma-channeler";
    public override string TemplateId => "named.karma-channeler";

    public override void OnCardsRecycledToMainDeck(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        int recycleEvents
    )
    {
        if (recycleEvents <= 0)
        {
            return;
        }

        var friendlyUnits = RiftboundEffectUnitTargeting.EnumerateFriendlyUnits(
            session,
            player.PlayerIndex
        );
        if (friendlyUnits.Count == 0)
        {
            return;
        }

        var buffedUnits = 0;
        for (var i = 0; i < recycleEvents; i += 1)
        {
            var target = friendlyUnits
                .OrderBy(x => x.PermanentMightModifier > 0 ? 1 : 0)
                .ThenByDescending(x => runtime.GetEffectiveMight(session, x))
                .ThenBy(x => x.Name, StringComparer.Ordinal)
                .ThenBy(x => x.InstanceId)
                .FirstOrDefault();
            if (target is null || target.PermanentMightModifier > 0)
            {
                continue;
            }

            target.PermanentMightModifier += 1;
            buffedUnits += 1;
        }

        if (buffedUnits <= 0)
        {
            return;
        }

        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "WhenRecycle",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["recycleEvents"] = recycleEvents.ToString(),
                ["buffedUnits"] = buffedUnits.ToString(),
            }
        );
    }
}
