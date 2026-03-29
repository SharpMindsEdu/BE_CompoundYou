using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class GarenMightOfDemaciaEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "garen-might-of-demacia";
    public override string TemplateId => "named.garen-might-of-demacia";

    public override void OnConquer(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        BattlefieldState battlefield,
        string? sourceActionId
    )
    {
        var friendlyCount = battlefield.Units.Count(x => x.ControllerPlayerIndex == player.PlayerIndex);
        if (friendlyCount < 4)
        {
            return;
        }

        runtime.DrawCards(player, 2);
        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "WhenConquer",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["battlefield"] = battlefield.Name,
                ["friendlyUnits"] = friendlyCount.ToString(),
                ["draw"] = "2",
            }
        );
    }
}

