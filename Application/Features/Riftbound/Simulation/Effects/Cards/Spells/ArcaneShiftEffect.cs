using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class ArcaneShiftEffect : RiftboundNamedCardEffectBase
{
    private const string BanishFriendlyUnitMarker = "-banish-friendly-unit-";

    public override string NameIdentifier => "arcane-shift";
    public override string TemplateId => "named.arcane-shift";

    public override bool TryAddLegalActions(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        List<RiftboundLegalAction> actions
    )
    {
        var friendlies = RiftboundEffectUnitTargeting.EnumerateFriendlyUnits(session, player.PlayerIndex);
        var enemyBattlefieldUnits = session.Battlefields
            .SelectMany(x => x.Units)
            .Where(x => x.ControllerPlayerIndex != player.PlayerIndex)
            .OrderBy(x => x.Name, StringComparer.Ordinal)
            .ThenBy(x => x.InstanceId)
            .ToList();
        foreach (var friendly in friendlies)
        {
            foreach (var enemy in enemyBattlefieldUnits)
            {
                actions.Add(
                    new RiftboundLegalAction(
                        $"{runtime.ActionPrefix}play-{card.InstanceId}-spell{BanishFriendlyUnitMarker}{friendly.InstanceId}-target-unit-{enemy.InstanceId}",
                        RiftboundActionType.PlayCard,
                        player.PlayerIndex,
                        $"Play {card.Name} banishing {friendly.Name} and damaging {enemy.Name}"
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
        var friendlyTarget = ResolveBanishFriendlyTarget(session, actionId);
        var enemyTarget = RiftboundEffectUnitTargeting.ResolveTargetUnitFromAction(session, actionId);
        if (
            friendlyTarget is null
            || enemyTarget is null
            || friendlyTarget.ControllerPlayerIndex != player.PlayerIndex
            || enemyTarget.ControllerPlayerIndex == player.PlayerIndex
        )
        {
            return;
        }

        var originalBattlefield = RiftboundEffectUnitTargeting.FindBattlefieldContainingUnit(
            session,
            friendlyTarget.InstanceId
        );
        var originalInBase = session.Players[friendlyTarget.ControllerPlayerIndex]
            .BaseZone.Cards.Any(x => x.InstanceId == friendlyTarget.InstanceId);

        RemoveUnitFromCurrentLocation(session, friendlyTarget);
        friendlyTarget.ControllerPlayerIndex = friendlyTarget.OwnerPlayerIndex;
        friendlyTarget.IsExhausted = true;
        if (originalBattlefield is not null && friendlyTarget.OwnerPlayerIndex == player.PlayerIndex)
        {
            originalBattlefield.Units.Add(friendlyTarget);
        }
        else
        {
            session.Players[friendlyTarget.OwnerPlayerIndex].BaseZone.Cards.Add(friendlyTarget);
        }

        var damage = 3 + runtime.GetSpellAndAbilityBonusDamage(session, player.PlayerIndex);
        enemyTarget.MarkedDamage += damage;

        card.EffectData["banishSelfAfterResolve"] = "true";
        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "Resolve",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["banishedAndReplayed"] = friendlyTarget.Name,
                ["wasInBase"] = originalInBase ? "true" : "false",
                ["damageTarget"] = enemyTarget.Name,
                ["damage"] = damage.ToString(),
                ["banishedSelf"] = "true",
            }
        );
    }

    private static CardInstance? ResolveBanishFriendlyTarget(GameSession session, string actionId)
    {
        var markerIndex = actionId.IndexOf(BanishFriendlyUnitMarker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return null;
        }

        var fragment = actionId[(markerIndex + BanishFriendlyUnitMarker.Length)..];
        var match = System.Text.RegularExpressions.Regex.Match(
            fragment,
            @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}"
        );
        if (!match.Success || !Guid.TryParse(match.Value, out var unitId))
        {
            return null;
        }

        return RiftboundEffectUnitTargeting.EnumerateAllUnits(session).FirstOrDefault(x =>
            x.InstanceId == unitId
        );
    }

    private static void RemoveUnitFromCurrentLocation(GameSession session, CardInstance unit)
    {
        foreach (var player in session.Players)
        {
            if (player.BaseZone.Cards.Remove(unit))
            {
                return;
            }
        }

        foreach (var battlefield in session.Battlefields)
        {
            if (battlefield.Units.Remove(unit))
            {
                return;
            }
        }
    }
}
