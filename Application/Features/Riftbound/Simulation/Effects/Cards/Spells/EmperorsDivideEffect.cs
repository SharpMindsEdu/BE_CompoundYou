using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class EmperorsDivideEffect : RiftboundNamedCardEffectBase
{
    private const string BattlefieldMarker = "-emperors-divide-bf-";

    public override string NameIdentifier => "emperor-s-divide";
    public override string TemplateId => "named.emperor-s-divide";

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
            var friendlyCount = battlefield.Units.Count(x => x.ControllerPlayerIndex == player.PlayerIndex);
            if (friendlyCount <= 0)
            {
                continue;
            }

            actions.Add(
                new RiftboundLegalAction(
                    $"{runtime.ActionPrefix}play-{card.InstanceId}-spell{BattlefieldMarker}{battlefield.Index}",
                    RiftboundActionType.PlayCard,
                    player.PlayerIndex,
                    $"Play {card.Name} moving {friendlyCount} units from {battlefield.Name} to base"
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

        var moved = battlefield.Units
            .Where(x => x.ControllerPlayerIndex == player.PlayerIndex)
            .ToList();
        foreach (var unit in moved)
        {
            battlefield.Units.Remove(unit);
            session.Players[unit.OwnerPlayerIndex].BaseZone.Cards.Add(unit);
        }
    }

    private static int? ResolveBattlefieldIndex(string actionId)
    {
        var markerIndex = actionId.IndexOf(BattlefieldMarker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return null;
        }

        var fragment = actionId[(markerIndex + BattlefieldMarker.Length)..];
        return int.TryParse(fragment, out var parsed) ? parsed : null;
    }
}

