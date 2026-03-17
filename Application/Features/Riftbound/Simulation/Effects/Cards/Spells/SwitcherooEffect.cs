using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class SwitcherooEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "switcheroo";
    public override string TemplateId => "named.switcheroo";

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
            var units = battlefield.Units.OrderBy(x => x.Name, StringComparer.Ordinal).ThenBy(x => x.InstanceId).ToList();
            for (var left = 0; left < units.Count; left += 1)
            {
                for (var right = left + 1; right < units.Count; right += 1)
                {
                    var targetList = string.Join(
                        ',',
                        new[] { units[left].InstanceId.ToString(), units[right].InstanceId.ToString() }
                    );
                    actions.Add(
                        new RiftboundLegalAction(
                            $"{runtime.ActionPrefix}play-{card.InstanceId}-spell{runtime.MultiTargetUnitsMarker}{targetList}",
                            RiftboundActionType.PlayCard,
                            player.PlayerIndex,
                            $"Play {card.Name} targeting {units[left].Name} and {units[right].Name}"
                        )
                    );
                }
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
        var targets = RiftboundEffectUnitTargeting.ResolveTargetUnitsFromAction(session, actionId).Take(2).ToList();
        if (targets.Count != 2)
        {
            return;
        }

        var firstBattlefield = RiftboundEffectUnitTargeting.FindBattlefieldContainingUnit(session, targets[0].InstanceId);
        var secondBattlefield = RiftboundEffectUnitTargeting.FindBattlefieldContainingUnit(session, targets[1].InstanceId);
        if (firstBattlefield is null || secondBattlefield is null || firstBattlefield.Index != secondBattlefield.Index)
        {
            return;
        }

        var leftCurrent = EffectiveMight(targets[0]);
        var rightCurrent = EffectiveMight(targets[1]);
        targets[0].TemporaryMightModifier += rightCurrent - leftCurrent;
        targets[1].TemporaryMightModifier += leftCurrent - rightCurrent;

        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "Resolve",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["left"] = targets[0].Name,
                ["right"] = targets[1].Name,
                ["battlefield"] = firstBattlefield.Name,
            }
        );
    }

    private static int EffectiveMight(CardInstance unit)
    {
        return unit.Might.GetValueOrDefault() + unit.PermanentMightModifier + unit.TemporaryMightModifier;
    }
}
