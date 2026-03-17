using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class DesertsCallEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "desert-s-call";
    public override string TemplateId => "named.desert-s-call";

    protected override IReadOnlyDictionary<string, string> BuildData(
        RiftboundCard card,
        string normalizedEffectText
    )
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["repeatEnergyCost"] = "2",
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
        var actionId = $"{runtime.ActionPrefix}play-{card.InstanceId}-spell";
        actions.Add(
            new RiftboundLegalAction(
                actionId,
                RiftboundActionType.PlayCard,
                player.PlayerIndex,
                $"Play spell {card.Name}"
            )
        );
        actions.Add(
            new RiftboundLegalAction(
                $"{actionId}{runtime.RepeatActionSuffix}",
                RiftboundActionType.PlayCard,
                player.PlayerIndex,
                $"Play spell {card.Name} (repeat)"
            )
        );
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
        player.BaseZone.Cards.Add(
            RiftboundTokenFactory.CreateSandSoldierUnitToken(
                ownerPlayerIndex: player.PlayerIndex,
                controllerPlayerIndex: player.PlayerIndex,
                might: 2,
                exhausted: true
            )
        );

        if (runtime.IsRepeatRequested(actionId) && runtime.TryPayRepeatCost(session, player, card))
        {
            player.BaseZone.Cards.Add(
                RiftboundTokenFactory.CreateSandSoldierUnitToken(
                    ownerPlayerIndex: player.PlayerIndex,
                    controllerPlayerIndex: player.PlayerIndex,
                    might: 2,
                    exhausted: true
                )
            );
        }
    }
}
