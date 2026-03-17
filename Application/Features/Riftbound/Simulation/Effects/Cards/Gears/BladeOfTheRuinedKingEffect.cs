using System.Text.RegularExpressions;
using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed partial class BladeOfTheRuinedKingEffect : RiftboundNamedCardEffectBase
{
    private const string SacrificeMarker = "-blade-sac-";

    [GeneratedRegex(
        @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}",
        RegexOptions.Compiled
    )]
    private static partial Regex GuidRegex();

    public override string NameIdentifier => "blade-of-the-ruined-king";
    public override string TemplateId => "named.blade-of-the-ruined-king";

    protected override IReadOnlyDictionary<string, string> BuildData(
        RiftboundCard card,
        string normalizedEffectText
    )
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["attachedMightBonus"] = "4",
        };
    }

    public override bool TryAddLegalActions(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        List<RiftboundLegalAction> actions
    )
    {
        var friendlyUnits = RiftboundEffectUnitTargeting.EnumerateFriendlyUnits(session, player.PlayerIndex).ToList();
        if (friendlyUnits.Count < 2)
        {
            return true;
        }

        foreach (var attachTarget in friendlyUnits)
        {
            foreach (var sacrifice in friendlyUnits.Where(x => x.InstanceId != attachTarget.InstanceId))
            {
                actions.Add(
                    new RiftboundLegalAction(
                        $"{runtime.ActionPrefix}play-{card.InstanceId}-spell-target-unit-{attachTarget.InstanceId}{SacrificeMarker}{sacrifice.InstanceId}",
                        RiftboundActionType.PlayCard,
                        player.PlayerIndex,
                        $"Play {card.Name}, attach to {attachTarget.Name}, kill {sacrifice.Name}"
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
        var targetUnit = RiftboundEffectUnitTargeting.ResolveTargetUnitFromAction(session, actionId);
        var sacrificeUnit = ResolveSacrificeUnit(session, actionId);
        if (
            targetUnit is null
            || targetUnit.ControllerPlayerIndex != player.PlayerIndex
            || sacrificeUnit is null
            || sacrificeUnit.ControllerPlayerIndex != player.PlayerIndex
        )
        {
            return;
        }

        RemoveUnitFromBoard(session, sacrificeUnit);
        var sacrificeOwner = session.Players.FirstOrDefault(x => x.PlayerIndex == sacrificeUnit.OwnerPlayerIndex);
        sacrificeOwner?.TrashZone.Cards.Add(sacrificeUnit);

        card.AttachedToInstanceId = targetUnit.InstanceId;
        card.IsExhausted = true;
        AddGearToTargetLocation(session, targetUnit, card);
        runtime.NotifyGearAttached(session, card, targetUnit);

        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "Resolve",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["target"] = targetUnit.Name,
                ["sacrificed"] = sacrificeUnit.Name,
            }
        );
    }

    private static CardInstance? ResolveSacrificeUnit(GameSession session, string actionId)
    {
        var markerIndex = actionId.IndexOf(SacrificeMarker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return null;
        }

        var fragment = actionId[(markerIndex + SacrificeMarker.Length)..];
        var match = GuidRegex().Match(fragment);
        if (!match.Success || !Guid.TryParse(match.Value, out var unitId))
        {
            return null;
        }

        return RiftboundEffectUnitTargeting.EnumerateAllUnits(session).FirstOrDefault(x => x.InstanceId == unitId);
    }

    private static void RemoveUnitFromBoard(GameSession session, CardInstance unit)
    {
        foreach (var currentPlayer in session.Players)
        {
            if (currentPlayer.BaseZone.Cards.Remove(unit))
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

    private static void AddGearToTargetLocation(
        GameSession session,
        CardInstance targetUnit,
        CardInstance gear
    )
    {
        var battlefield = RiftboundEffectUnitTargeting.FindBattlefieldContainingUnit(session, targetUnit.InstanceId);
        if (battlefield is not null)
        {
            battlefield.Gear.Add(gear);
            return;
        }

        var owner = session.Players.FirstOrDefault(x => x.PlayerIndex == targetUnit.ControllerPlayerIndex);
        owner?.BaseZone.Cards.Add(gear);
    }
}

