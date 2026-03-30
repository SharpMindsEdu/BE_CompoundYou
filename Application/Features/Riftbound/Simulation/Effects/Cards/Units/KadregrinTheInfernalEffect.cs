using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class KadregrinTheInfernalEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "kadregrin-the-infernal";
    public override string TemplateId => "named.kadregrin-the-infernal";

    public override void OnUnitPlay(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        string actionId
    )
    {
        var mightyUnits = RiftboundEffectUnitTargeting.EnumerateFriendlyUnits(session, player.PlayerIndex)
            .Count(x => runtime.GetEffectiveMight(session, x) >= 5);
        if (mightyUnits <= 0)
        {
            return;
        }

        runtime.DrawCards(player, mightyUnits);
        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "WhenPlay",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["mightyUnits"] = mightyUnits.ToString(),
                ["drawn"] = mightyUnits.ToString(),
            }
        );
    }
}
