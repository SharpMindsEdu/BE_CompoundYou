using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class BloodMoneyEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "blood-money";
    public override string TemplateId => "named.blood-money";

    public override bool TryAddLegalActions(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        List<RiftboundLegalAction> actions
    )
    {
        foreach (
            var target in session.Battlefields.SelectMany(x => x.Units).Where(x =>
                runtime.GetEffectiveMight(session, x) <= 2
            )
        )
        {
            actions.Add(
                new RiftboundLegalAction(
                    $"{runtime.ActionPrefix}play-{card.InstanceId}-spell-target-unit-{target.InstanceId}",
                    RiftboundActionType.PlayCard,
                    player.PlayerIndex,
                    $"Play {card.Name} targeting {target.Name}"
                )
            );
        }

        return true;
    }

    public override void OnSpellOrGearPlay(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        string actionId
    )
    {
        var target = RiftboundEffectUnitTargeting.ResolveTargetUnitFromAction(session, actionId);
        if (
            target is null
            || RiftboundEffectUnitTargeting.FindBattlefieldContainingUnit(session, target.InstanceId) is null
        )
        {
            return;
        }

        var targetMight = runtime.GetEffectiveMight(session, target);
        if (targetMight > 2)
        {
            return;
        }

        target.MarkedDamage = targetMight;
        var goldToPlay = target.ControllerPlayerIndex == player.PlayerIndex ? 2 : 1;
        for (var i = 0; i < goldToPlay; i += 1)
        {
            player.BaseZone.Cards.Add(
                RiftboundTokenFactory.CreateGoldGearToken(
                    ownerPlayerIndex: player.PlayerIndex,
                    controllerPlayerIndex: player.PlayerIndex,
                    exhausted: true
                )
            );
        }

        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "Resolve",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["target"] = target.Name,
                ["playedGoldTokens"] = goldToPlay.ToString(),
            }
        );
    }
}

