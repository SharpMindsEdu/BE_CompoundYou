using Application.Features.Riftbound.Simulation.Engine;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class LastStandEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "last-stand";
    public override string TemplateId => "named.last-stand";

    public override bool TryAddLegalActions(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        List<RiftboundLegalAction> actions
    )
    {
        foreach (
            var target in RiftboundEffectUnitTargeting.EnumerateFriendlyUnits(
                session,
                player.PlayerIndex
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
        if (target is null || target.ControllerPlayerIndex != player.PlayerIndex)
        {
            return;
        }

        var currentMight = Math.Max(0, runtime.GetEffectiveMight(session, target));
        target.TemporaryMightModifier += currentMight;
        target.EffectData["temporaryUntilBeginning"] = "true";

        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "Resolve",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["target"] = target.Name,
                ["temporary"] = "true",
                ["addedMight"] = currentMight.ToString(),
            }
        );
    }
}
