using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class GuardsEffect : RiftboundNamedCardEffectBase
{
    public const string ReadyMarker = "-guards-ready";

    public override string NameIdentifier => "guards";
    public override string TemplateId => "named.guards";

    public override bool TryAddLegalActions(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        List<RiftboundLegalAction> actions
    )
    {
        actions.Add(
            new RiftboundLegalAction(
                $"{runtime.ActionPrefix}play-{card.InstanceId}-spell",
                RiftboundActionType.PlayCard,
                player.PlayerIndex,
                $"Play spell {card.Name}"
            )
        );
        actions.Add(
            new RiftboundLegalAction(
                $"{runtime.ActionPrefix}play-{card.InstanceId}-spell{ReadyMarker}",
                RiftboundActionType.PlayCard,
                player.PlayerIndex,
                $"Play spell {card.Name} (pay [Order] to ready token)"
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
        var token = RiftboundTokenFactory.CreateSandSoldierUnitToken(
            ownerPlayerIndex: player.PlayerIndex,
            controllerPlayerIndex: player.PlayerIndex,
            might: 2,
            exhausted: true
        );
        var readied = false;
        if (
            actionId.Contains(ReadyMarker, StringComparison.Ordinal)
            && runtime.TryPayCost(
                session,
                player,
                energyCost: 0,
                [new EffectPowerRequirement(1, ["Order"])]
            )
        )
        {
            token.IsExhausted = false;
            readied = true;
        }

        player.BaseZone.Cards.Add(token);
        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "Resolve",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["playedToken"] = "true",
                ["readiedToken"] = readied.ToString().ToLowerInvariant(),
            }
        );
    }
}

