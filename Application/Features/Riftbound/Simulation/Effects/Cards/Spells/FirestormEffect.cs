using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class FirestormEffect : RiftboundNamedCardEffectBase
{
    private const string BattlefieldMarker = "-firestorm-bf-";

    public override string NameIdentifier => "firestorm";
    public override string TemplateId => "named.firestorm";

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
            if (!battlefield.Units.Any(x => x.ControllerPlayerIndex != player.PlayerIndex))
            {
                continue;
            }

            actions.Add(
                new RiftboundLegalAction(
                    $"{runtime.ActionPrefix}play-{card.InstanceId}-spell{BattlefieldMarker}{battlefield.Index}",
                    RiftboundActionType.PlayCard,
                    player.PlayerIndex,
                    $"Play {card.Name} at battlefield {battlefield.Name}"
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

        var magnitude = runtime.ReadMagnitude(card, fallback: 3)
            + runtime.GetSpellAndAbilityBonusDamage(session, player.PlayerIndex);
        foreach (var enemy in battlefield.Units.Where(x => x.ControllerPlayerIndex != player.PlayerIndex))
        {
            enemy.MarkedDamage += magnitude;
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
