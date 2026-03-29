using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class GoldEffect : RiftboundNamedCardEffectBase
{
    private const string GenericRuneDomain = "__unknown__";

    public override string NameIdentifier => "gold";
    public override string TemplateId => "named.gold";
    public override bool HasActivatedAbility => true;

    public override bool TryActivateAbility(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card
    )
    {
        if (card.IsExhausted)
        {
            return false;
        }

        if (!RiftboundEffectGearTargeting.RemoveGearFromBoard(session, card))
        {
            return false;
        }

        card.IsExhausted = true;
        card.AttachedToInstanceId = null;
        var owner = session.Players.FirstOrDefault(x => x.PlayerIndex == card.OwnerPlayerIndex)
            ?? player;
        owner.TrashZone.Cards.Add(card);
        runtime.AddPower(player, GenericRuneDomain, 1);
        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "Activate",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["killedSelf"] = "true",
                ["addedRune"] = "1",
            }
        );
        return true;
    }
}

