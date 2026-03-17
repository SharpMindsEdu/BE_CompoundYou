using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class FaithfulManufactorEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "faithful-manufactor";
    public override string TemplateId => "named.faithful-manufactor";

    public override void OnUnitPlay(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        string actionId
    )
    {
        var token = RiftboundTokenFactory.CreateRecruitUnitToken(
            ownerPlayerIndex: player.PlayerIndex,
            controllerPlayerIndex: player.PlayerIndex,
            might: 1,
            exhausted: true
        );
        var battlefield = RiftboundEffectUnitTargeting.FindBattlefieldContainingUnit(session, card.InstanceId);
        if (battlefield is not null)
        {
            battlefield.Units.Add(token);
        }
        else
        {
            player.BaseZone.Cards.Add(token);
        }

        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "WhenPlay",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["playedRecruitToken"] = "true",
                ["location"] = battlefield is null ? "base" : $"bf-{battlefield.Index}",
            }
        );
    }
}
