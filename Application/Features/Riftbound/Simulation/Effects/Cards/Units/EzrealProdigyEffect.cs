using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class EzrealProdigyEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "ezreal-prodigy";
    public override string TemplateId => "named.ezreal-prodigy";

    public override void OnUnitPlay(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        string actionId
    )
    {
        CardInstance? discarded = null;
        if (player.HandZone.Cards.Count > 0)
        {
            discarded = player.HandZone.Cards
                .OrderBy(x => x.Cost.GetValueOrDefault())
                .ThenBy(x => x.Power.GetValueOrDefault())
                .ThenBy(x => x.Might.GetValueOrDefault())
                .ThenBy(x => x.Name, StringComparer.Ordinal)
                .ThenBy(x => x.InstanceId)
                .First();
            runtime.DiscardFromHand(session, player, discarded, reason: "EzrealWhenPlay", sourceCard: card);
        }

        runtime.DrawCards(player, 2);

        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["template"] = card.EffectTemplateId,
            ["drawn"] = "2",
        };
        if (discarded is not null)
        {
            metadata["discarded"] = discarded.Name;
        }

        runtime.AddEffectContext(session, card.Name, player.PlayerIndex, "WhenPlay", metadata);
    }
}
