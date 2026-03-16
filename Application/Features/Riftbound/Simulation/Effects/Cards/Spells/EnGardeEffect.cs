using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class EnGardeEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "en-garde";
    public override string TemplateId => "named.en-garde";

    protected override IReadOnlyDictionary<string, string> BuildData(
        RiftboundCard card,
        string normalizedEffectText
    )
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["baseMagnitude"] = "1",
            ["bonusMagnitudeWhenOnlyFriendlyThere"] = "1",
        };
    }

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

        var baseMagnitude = runtime.ReadIntEffectData(card, "baseMagnitude", fallback: 1);
        var bonusMagnitude = runtime.ReadIntEffectData(
            card,
            "bonusMagnitudeWhenOnlyFriendlyThere",
            fallback: 1
        );
        var friendlyCountAtLocation = RiftboundEffectUnitTargeting.CountFriendlyUnitsAtSameLocation(
            session,
            target,
            player.PlayerIndex
        );
        var isOnlyFriendlyThere = friendlyCountAtLocation == 1;
        var totalBuff = baseMagnitude + (isOnlyFriendlyThere ? bonusMagnitude : 0);
        target.TemporaryMightModifier += totalBuff;

        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "Resolve",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["target"] = target.Name,
                ["buff"] = totalBuff.ToString(),
                ["onlyFriendlyThere"] = isOnlyFriendlyThere ? "true" : "false",
            }
        );
    }
}
