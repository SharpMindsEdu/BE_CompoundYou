using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class CatalystOfAeonsEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "catalyst-of-aeons";
    public override string TemplateId => "named.catalyst-of-aeons";

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
        var channeled = 0;
        for (var i = 0; i < 2; i += 1)
        {
            if (player.RuneDeckZone.Cards.Count == 0)
            {
                break;
            }

            var rune = player.RuneDeckZone.Cards[0];
            player.RuneDeckZone.Cards.RemoveAt(0);
            rune.IsExhausted = true;
            player.BaseZone.Cards.Add(rune);
            channeled += 1;
        }

        if (channeled < 2)
        {
            runtime.DrawCards(player, 1);
        }

        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "Resolve",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["channeled"] = channeled.ToString(),
                ["drew"] = channeled < 2 ? "true" : "false",
            }
        );
    }
}
