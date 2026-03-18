using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class ForgeOfTheFutureEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "forge-of-the-future";
    public override string TemplateId => "named.forge-of-the-future";
    public override bool HasActivatedAbility => true;

    public override void OnSpellOrGearPlay(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        string actionId
    )
    {
        player.BaseZone.Cards.Add(
            RiftboundTokenFactory.CreateRecruitUnitToken(
                ownerPlayerIndex: player.PlayerIndex,
                controllerPlayerIndex: player.PlayerIndex,
                might: 1,
                exhausted: true
            )
        );
    }

    public override bool TryActivateAbility(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card
    )
    {
        if (!RemoveFromCurrentLocation(session, card))
        {
            return false;
        }

        session.Players[card.OwnerPlayerIndex].TrashZone.Cards.Add(card);
        var recycled = 0;
        var targets = player.TrashZone.Cards
            .Where(x => x.InstanceId != card.InstanceId)
            .OrderBy(x => x.Name, StringComparer.Ordinal)
            .ThenBy(x => x.InstanceId)
            .Take(4)
            .ToList();
        foreach (var target in targets)
        {
            if (!player.TrashZone.Cards.Remove(target))
            {
                continue;
            }

            player.MainDeckZone.Cards.Add(target);
            recycled += 1;
        }

        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "Activate",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["recycled"] = recycled.ToString(),
            }
        );
        return true;
    }

    private static bool RemoveFromCurrentLocation(GameSession session, CardInstance card)
    {
        foreach (var player in session.Players)
        {
            if (player.BaseZone.Cards.Remove(card))
            {
                return true;
            }
        }

        foreach (var battlefield in session.Battlefields)
        {
            if (battlefield.Gear.Remove(card))
            {
                return true;
            }
        }

        return false;
    }
}
