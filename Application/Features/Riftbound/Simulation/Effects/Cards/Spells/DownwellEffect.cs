using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class DownwellEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "downwell";
    public override string TemplateId => "named.downwell";

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
        foreach (var currentPlayer in session.Players)
        {
            var baseCards = currentPlayer.BaseZone.Cards
                .Where(x =>
                    string.Equals(x.Type, "Unit", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(x.Type, "Gear", StringComparison.OrdinalIgnoreCase))
                .ToList();
            foreach (var boardCard in baseCards)
            {
                currentPlayer.BaseZone.Cards.Remove(boardCard);
                session.Players[boardCard.OwnerPlayerIndex].HandZone.Cards.Add(boardCard);
            }
        }

        foreach (var battlefield in session.Battlefields)
        {
            foreach (var unit in battlefield.Units.ToList())
            {
                battlefield.Units.Remove(unit);
                session.Players[unit.OwnerPlayerIndex].HandZone.Cards.Add(unit);
            }

            foreach (var gear in battlefield.Gear.ToList())
            {
                battlefield.Gear.Remove(gear);
                gear.AttachedToInstanceId = null;
                session.Players[gear.OwnerPlayerIndex].HandZone.Cards.Add(gear);
            }
        }
    }
}

