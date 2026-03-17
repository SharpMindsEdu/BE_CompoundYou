using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class DefiantDanceEffect : RiftboundNamedCardEffectBase
{
    private const string BuffTargetMarker = "-defiant-buff-";

    public override string NameIdentifier => "defiant-dance";
    public override string TemplateId => "named.defiant-dance";

    public override bool TryAddLegalActions(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        List<RiftboundLegalAction> actions
    )
    {
        var units = RiftboundEffectUnitTargeting.EnumerateAllUnits(session).ToList();
        foreach (var buffTarget in units)
        {
            foreach (var debuffTarget in units.Where(x => x.InstanceId != buffTarget.InstanceId))
            {
                actions.Add(
                    new RiftboundLegalAction(
                        $"{runtime.ActionPrefix}play-{card.InstanceId}-spell{BuffTargetMarker}{buffTarget.InstanceId}-target-unit-{debuffTarget.InstanceId}",
                        RiftboundActionType.PlayCard,
                        player.PlayerIndex,
                        $"Play {card.Name}: +2 {buffTarget.Name}, -2 {debuffTarget.Name}"
                    )
                );
            }
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
        var buffTarget = ResolveBuffTarget(session, actionId);
        var debuffTarget = RiftboundEffectUnitTargeting.ResolveTargetUnitFromAction(session, actionId);
        if (
            buffTarget is null
            || debuffTarget is null
            || buffTarget.InstanceId == debuffTarget.InstanceId
        )
        {
            return;
        }

        buffTarget.TemporaryMightModifier += 2;
        debuffTarget.TemporaryMightModifier -= 2;
    }

    private static CardInstance? ResolveBuffTarget(GameSession session, string actionId)
    {
        var markerIndex = actionId.IndexOf(BuffTargetMarker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return null;
        }

        var fragment = actionId[(markerIndex + BuffTargetMarker.Length)..];
        var guidText = fragment.Length >= 36 ? fragment[..36] : string.Empty;
        if (!Guid.TryParse(guidText, out var id))
        {
            return null;
        }

        return RiftboundEffectUnitTargeting.EnumerateAllUnits(session).FirstOrDefault(x => x.InstanceId == id);
    }
}

