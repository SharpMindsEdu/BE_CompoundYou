using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class FoxFireEffect : RiftboundNamedCardEffectBase
{
    private const string BattlefieldMarker = "-fox-fire-bf-";

    public override string NameIdentifier => "fox-fire";
    public override string TemplateId => "named.fox-fire";

    public override bool TryAddLegalActions(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        List<RiftboundLegalAction> actions
    )
    {
        foreach (var battlefield in session.Battlefields)
        {
            var units = battlefield.Units
                .OrderBy(x => x.Name, StringComparer.Ordinal)
                .ThenBy(x => x.InstanceId)
                .ToList();
            if (units.Count == 0)
            {
                continue;
            }

            foreach (var combination in BuildCombinations(runtime, session, units, maxTotalMight: 4))
            {
                var targetList = string.Join(',', combination.Select(x => x.InstanceId));
                actions.Add(
                    new RiftboundLegalAction(
                        $"{runtime.ActionPrefix}play-{card.InstanceId}-spell{BattlefieldMarker}{battlefield.Index}{runtime.MultiTargetUnitsMarker}{targetList}",
                        RiftboundActionType.PlayCard,
                        player.PlayerIndex,
                        $"Play {card.Name} at {battlefield.Name} targeting {combination.Count} unit(s)"
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
        var battlefieldIndex = ResolveBattlefieldIndex(actionId);
        if (battlefieldIndex is null)
        {
            return;
        }

        var battlefield = session.Battlefields.FirstOrDefault(x => x.Index == battlefieldIndex.Value);
        if (battlefield is null)
        {
            return;
        }

        var targets = RiftboundEffectUnitTargeting.ResolveTargetUnitsFromAction(session, actionId)
            .Where(x => battlefield.Units.Any(unit => unit.InstanceId == x.InstanceId))
            .DistinctBy(x => x.InstanceId)
            .ToList();
        if (targets.Count == 0)
        {
            return;
        }

        var totalMight = targets.Sum(x => Math.Max(0, runtime.GetEffectiveMight(session, x)));
        if (totalMight > 4)
        {
            return;
        }

        foreach (var target in targets)
        {
            battlefield.Units.Remove(target);
            session.Players[target.OwnerPlayerIndex].TrashZone.Cards.Add(target);
        }
    }

    private static IReadOnlyCollection<IReadOnlyCollection<CardInstance>> BuildCombinations(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        IReadOnlyList<CardInstance> units,
        int maxTotalMight
    )
    {
        var result = new List<IReadOnlyCollection<CardInstance>>();
        var current = new List<CardInstance>();

        void Recurse(int index, int total)
        {
            if (total > maxTotalMight)
            {
                return;
            }

            if (index >= units.Count)
            {
                if (current.Count > 0)
                {
                    result.Add(current.ToList());
                }

                return;
            }

            Recurse(index + 1, total);

            var next = units[index];
            var might = Math.Max(0, runtime.GetEffectiveMight(session, next));
            current.Add(next);
            Recurse(index + 1, total + might);
            current.RemoveAt(current.Count - 1);
        }

        Recurse(0, 0);
        return result;
    }

    private static int? ResolveBattlefieldIndex(string actionId)
    {
        var markerIndex = actionId.IndexOf(BattlefieldMarker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return null;
        }

        var fragment = actionId[(markerIndex + BattlefieldMarker.Length)..];
        var targetsIndex = fragment.IndexOf("-target-units-", StringComparison.Ordinal);
        if (targetsIndex >= 0)
        {
            fragment = fragment[..targetsIndex];
        }

        return int.TryParse(fragment, out var parsed) ? parsed : null;
    }
}
