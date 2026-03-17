using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class BrynhirThundersongEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "brynhir-thundersong";
    public override string TemplateId => "named.brynhir-thundersong";

    public override void OnUnitPlay(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        string actionId
    )
    {
        var opponent = session.Players.FirstOrDefault(x => x.PlayerIndex != player.PlayerIndex);
        if (opponent is null)
        {
            return;
        }

        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "Aura",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["lockOpponentCardPlayThisTurn"] = "true",
                ["affectedPlayerIndex"] = opponent.PlayerIndex.ToString(),
                ["turn"] = session.TurnNumber.ToString(),
            }
        );
    }
}

