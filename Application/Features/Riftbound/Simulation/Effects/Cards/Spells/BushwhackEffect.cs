using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class BushwhackEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "bushwhack";
    public override string TemplateId => "named.bushwhack";

    public override void OnSpellOrGearPlay(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        string actionId
    )
    {
        player.BaseZone.Cards.Add(
            RiftboundTokenFactory.CreateGoldGearToken(
                ownerPlayerIndex: player.PlayerIndex,
                controllerPlayerIndex: player.PlayerIndex,
                exhausted: true
            )
        );

        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "Aura",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["friendlyUnitsEnterReadyThisTurn"] = "true",
                ["turn"] = session.TurnNumber.ToString(),
            }
        );

        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "Resolve",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["playedGoldToken"] = "true",
            }
        );
    }
}

