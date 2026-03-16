using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class AcceptableLossesEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "acceptable-losses";
    public override string TemplateId => "named.acceptable-losses";

    public override bool TryAddLegalActions(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        List<RiftboundLegalAction> actions
    )
    {
        var friendlyGear = RiftboundEffectGearTargeting.EnumerateControlledGear(
            session,
            player.PlayerIndex
        );
        if (friendlyGear.Count == 0)
        {
            actions.Add(
                new RiftboundLegalAction(
                    $"{runtime.ActionPrefix}play-{card.InstanceId}-spell",
                    RiftboundActionType.PlayCard,
                    player.PlayerIndex,
                    $"Play spell {card.Name}"
                )
            );
            return true;
        }

        foreach (var gear in friendlyGear)
        {
            actions.Add(
                new RiftboundLegalAction(
                    $"{runtime.ActionPrefix}play-{card.InstanceId}-spell-target-gear-{gear.InstanceId}",
                    RiftboundActionType.PlayCard,
                    player.PlayerIndex,
                    $"Play {card.Name} and kill {gear.Name}"
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
        var opponentIndex = session
            .Players.Select(x => x.PlayerIndex)
            .FirstOrDefault(x => x != player.PlayerIndex);

        var selectedFriendlyGear = RiftboundEffectGearTargeting.ResolveTargetGearFromAction(
            session,
            actionId
        );
        if (
            selectedFriendlyGear is not null
            && selectedFriendlyGear.ControllerPlayerIndex != player.PlayerIndex
        )
        {
            selectedFriendlyGear = null;
        }

        selectedFriendlyGear ??= RiftboundEffectGearTargeting
            .EnumerateControlledGear(session, player.PlayerIndex)
            .FirstOrDefault();
        var selectedOpponentGear = RiftboundEffectGearTargeting
            .EnumerateControlledGear(session, opponentIndex)
            .FirstOrDefault();

        var killedFriendly = KillGear(session, selectedFriendlyGear);
        var killedOpponent = KillGear(session, selectedOpponentGear);

        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "Resolve",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["killedFriendlyGear"] = killedFriendly ? "true" : "false",
                ["killedOpponentGear"] = killedOpponent ? "true" : "false",
                ["friendlyGear"] = selectedFriendlyGear?.Name ?? string.Empty,
                ["opponentGear"] = selectedOpponentGear?.Name ?? string.Empty,
            }
        );
    }

    private static bool KillGear(GameSession session, CardInstance? gear)
    {
        if (gear is null)
        {
            return false;
        }

        if (!RiftboundEffectGearTargeting.RemoveGearFromBoard(session, gear))
        {
            return false;
        }

        gear.AttachedToInstanceId = null;
        var owner = RiftboundEffectGearTargeting.ResolveOwnerPlayer(session, gear);
        owner.TrashZone.Cards.Add(gear);
        return true;
    }
}
