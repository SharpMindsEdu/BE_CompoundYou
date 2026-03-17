using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class DazzlingAuroraEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "dazzling-aurora";
    public override string TemplateId => "named.dazzling-aurora";

    public override void OnEndTurn(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card
    )
    {
        var revealedNonUnits = new List<CardInstance>();
        CardInstance? revealedUnit = null;
        while (player.MainDeckZone.Cards.Count > 0)
        {
            var next = player.MainDeckZone.Cards[0];
            if (string.Equals(next.Type, "Unit", StringComparison.OrdinalIgnoreCase))
            {
                revealedUnit = next;
                break;
            }

            player.MainDeckZone.Cards.RemoveAt(0);
            revealedNonUnits.Add(next);
        }

        if (revealedUnit is not null)
        {
            runtime.TryPlayCardFromRevealIgnoringCost(session, player, revealedUnit, card);
        }

        foreach (var recycled in revealedNonUnits)
        {
            player.TrashZone.Cards.Add(recycled);
        }
    }
}
