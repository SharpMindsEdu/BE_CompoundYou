using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class BaitedHookEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "baited-hook";
    public override string TemplateId => "named.baited-hook";
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

        var killTarget = RiftboundEffectUnitTargeting.EnumerateFriendlyUnits(session, player.PlayerIndex)
            .OrderByDescending(x => x.Might.GetValueOrDefault())
            .ThenByDescending(x => x.Cost.GetValueOrDefault())
            .ThenBy(x => x.Name, StringComparer.Ordinal)
            .ThenBy(x => x.InstanceId)
            .FirstOrDefault();
        if (killTarget is null)
        {
            return false;
        }

        if (
            !runtime.TryPayCost(
                session,
                player,
                energyCost: 1,
                [new EffectPowerRequirement(1, ["Order"])]
            )
        )
        {
            return false;
        }

        card.IsExhausted = true;
        var killedMight = killTarget.Might.GetValueOrDefault()
            + killTarget.PermanentMightModifier
            + killTarget.TemporaryMightModifier;
        DestroyFriendlyUnit(session, player, killTarget);

        var lookedCards = player.MainDeckZone.Cards.Take(5).ToList();
        var revealEnergyAdded = 0;
        foreach (var looked in lookedCards.ToList())
        {
            if (!player.MainDeckZone.Cards.Contains(looked))
            {
                continue;
            }

            var reveal = runtime.ResolveTopDeckRevealEffects(session, player, looked, card);
            revealEnergyAdded += reveal.AddedEnergy;
        }

        var remaining = lookedCards.Where(x => player.MainDeckZone.Cards.Contains(x)).ToList();
        var limit = killedMight + 1;
        var playable = remaining
            .Where(x =>
                string.Equals(x.Type, "Unit", StringComparison.OrdinalIgnoreCase)
                && x.Might.GetValueOrDefault() <= limit
            )
            .OrderByDescending(x => x.Might.GetValueOrDefault())
            .ThenByDescending(x => x.Cost.GetValueOrDefault())
            .ThenBy(x => x.Name, StringComparer.Ordinal)
            .ThenBy(x => x.InstanceId)
            .FirstOrDefault();

        var playedName = string.Empty;
        if (
            playable is not null
            && runtime.TryPlayCardFromRevealIgnoringCost(
                session,
                player,
                playable,
                card,
                preferredBattlefieldIndex: null
            )
        )
        {
            playedName = playable.Name;
            remaining.Remove(playable);
        }

        var recycledCount = 0;
        foreach (var recycle in remaining)
        {
            if (!player.MainDeckZone.Cards.Remove(recycle))
            {
                continue;
            }

            player.MainDeckZone.Cards.Add(recycle);
            recycledCount += 1;
        }

        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "Activate",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["killedUnit"] = killTarget.Name,
                ["killedMight"] = killedMight.ToString(),
                ["looked"] = lookedCards.Count.ToString(),
                ["limit"] = limit.ToString(),
                ["played"] = playedName,
                ["recycled"] = recycledCount.ToString(),
                ["revealEnergyAdded"] = revealEnergyAdded.ToString(),
            }
        );

        return true;
    }

    private static void DestroyFriendlyUnit(GameSession session, PlayerState player, CardInstance unit)
    {
        if (!player.BaseZone.Cards.Remove(unit))
        {
            foreach (var battlefield in session.Battlefields)
            {
                if (battlefield.Units.Remove(unit))
                {
                    break;
                }
            }
        }

        var attachedGear = RiftboundEffectGearTargeting.EnumerateAllGear(session)
            .Where(x => x.AttachedToInstanceId == unit.InstanceId)
            .ToList();
        foreach (var gear in attachedGear)
        {
            if (!RiftboundEffectGearTargeting.RemoveGearFromBoard(session, gear))
            {
                continue;
            }

            gear.AttachedToInstanceId = null;
            var owner = RiftboundEffectGearTargeting.ResolveOwnerPlayer(session, gear);
            owner.TrashZone.Cards.Add(gear);
        }

        player.TrashZone.Cards.Add(unit);
    }
}

