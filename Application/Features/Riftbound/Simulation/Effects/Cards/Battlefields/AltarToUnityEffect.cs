using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class AltarToUnityEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "altar-to-unity";
    public override string TemplateId => "named.altar-to-unity";

    public override void OnHoldScore(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        BattlefieldState battlefield
    )
    {
        player.BaseZone.Cards.Add(
            RiftboundTokenFactory.CreateRecruitUnitToken(
                ownerPlayerIndex: player.PlayerIndex,
                controllerPlayerIndex: player.PlayerIndex,
                might: 1,
                exhausted: true
            )
        );

        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "WhenHold",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["battlefield"] = battlefield.Name,
                ["playedRecruitToken"] = "true",
            }
        );
    }
}

