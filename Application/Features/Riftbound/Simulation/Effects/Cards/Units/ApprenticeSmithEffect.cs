using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class ApprenticeSmithEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "apprentice-smith";
    public override string TemplateId => "named.apprentice-smith";

    public override void OnUnitMove(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card
    )
    {
        if (player.MainDeckZone.Cards.Count == 0)
        {
            return;
        }

        var topCard = player.MainDeckZone.Cards[0];
        var isGear = string.Equals(topCard.Type, "Gear", StringComparison.OrdinalIgnoreCase);
        if (isGear)
        {
            player.MainDeckZone.Cards.RemoveAt(0);
            player.HandZone.Cards.Add(topCard);
        }
        else
        {
            player.MainDeckZone.Cards.RemoveAt(0);
            player.MainDeckZone.Cards.Add(topCard);
        }

        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "WhenMove",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["revealed"] = topCard.Name,
                ["drew"] = isGear ? "true" : "false",
                ["recycled"] = isGear ? "false" : "true",
            }
        );
    }
}
