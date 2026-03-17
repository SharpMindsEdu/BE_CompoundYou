using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class DeathgripEffect : RiftboundNamedCardEffectBase
{
    private const string SacrificeMarker = "-deathgrip-sac-";

    public override string NameIdentifier => "deathgrip";
    public override string TemplateId => "named.deathgrip";

    public override bool TryAddLegalActions(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        List<RiftboundLegalAction> actions
    )
    {
        var friendly = RiftboundEffectUnitTargeting.EnumerateFriendlyUnits(session, player.PlayerIndex).ToList();
        foreach (var sacrifice in friendly)
        {
            foreach (var target in friendly.Where(x => x.InstanceId != sacrifice.InstanceId))
            {
                actions.Add(
                    new RiftboundLegalAction(
                        $"{runtime.ActionPrefix}play-{card.InstanceId}-spell{SacrificeMarker}{sacrifice.InstanceId}-target-unit-{target.InstanceId}",
                        RiftboundActionType.PlayCard,
                        player.PlayerIndex,
                        $"Play {card.Name}: kill {sacrifice.Name}, buff {target.Name}"
                    )
                );
            }
        }

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
        var sacrifice = ResolveSacrifice(session, actionId);
        var target = RiftboundEffectUnitTargeting.ResolveTargetUnitFromAction(session, actionId);
        if (
            sacrifice is null
            || target is null
            || sacrifice.ControllerPlayerIndex != player.PlayerIndex
            || target.ControllerPlayerIndex != player.PlayerIndex
            || sacrifice.InstanceId == target.InstanceId
        )
        {
            return;
        }

        var sacrificeMight = runtime.GetEffectiveMight(session, sacrifice);
        if (!TryKillUnit(session, sacrifice))
        {
            return;
        }

        target.TemporaryMightModifier += Math.Max(0, sacrificeMight);
        runtime.DrawCards(player, 1);
    }

    private static CardInstance? ResolveSacrifice(GameSession session, string actionId)
    {
        var markerIndex = actionId.IndexOf(SacrificeMarker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return null;
        }

        var fragment = actionId[(markerIndex + SacrificeMarker.Length)..];
        var guidText = fragment.Length >= 36 ? fragment[..36] : string.Empty;
        if (!Guid.TryParse(guidText, out var id))
        {
            return null;
        }

        return RiftboundEffectUnitTargeting.EnumerateAllUnits(session).FirstOrDefault(x => x.InstanceId == id);
    }

    private static bool TryKillUnit(GameSession session, CardInstance unit)
    {
        if (!RemoveUnitFromBoard(session, unit))
        {
            return false;
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
            var gearOwner = RiftboundEffectGearTargeting.ResolveOwnerPlayer(session, gear);
            gearOwner.TrashZone.Cards.Add(gear);
        }

        var owner = session.Players.FirstOrDefault(x => x.PlayerIndex == unit.OwnerPlayerIndex)
            ?? session.Players[0];
        owner.TrashZone.Cards.Add(unit);
        return true;
    }

    private static bool RemoveUnitFromBoard(GameSession session, CardInstance unit)
    {
        foreach (var player in session.Players)
        {
            if (player.BaseZone.Cards.Remove(unit))
            {
                return true;
            }
        }

        foreach (var battlefield in session.Battlefields)
        {
            if (battlefield.Units.Remove(unit))
            {
                return true;
            }
        }

        return false;
    }
}
