using System.Text.RegularExpressions;
using Application.Features.Riftbound.Simulation.Engine;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed partial class LegionQuartermasterEffect : RiftboundNamedCardEffectBase
{
    public const string ReturnGearMarker = "-legion-quartermaster-return-";

    public override string NameIdentifier => "legion-quartermaster";
    public override string TemplateId => "named.legion-quartermaster";

    [GeneratedRegex(
        @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}",
        RegexOptions.Compiled
    )]
    private static partial Regex GuidRegex();

    public override bool TryAddUnitPlayLegalActions(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        List<RiftboundLegalAction> actions
    )
    {
        var friendlyGear = RiftboundEffectGearTargeting.EnumerateControlledGear(
            session,
            player.PlayerIndex
        ).ToList();
        if (friendlyGear.Count == 0)
        {
            return true;
        }

        foreach (var gear in friendlyGear)
        {
            actions.Add(
                new RiftboundLegalAction(
                    $"{runtime.ActionPrefix}play-{card.InstanceId}{ReturnGearMarker}{gear.InstanceId}-to-base",
                    RiftboundActionType.PlayCard,
                    player.PlayerIndex,
                    $"Play {card.Name} to base [return {gear.Name}]"
                )
            );

            foreach (var battlefield in session.Battlefields.Where(x => x.ControlledByPlayerIndex == player.PlayerIndex))
            {
                actions.Add(
                    new RiftboundLegalAction(
                        $"{runtime.ActionPrefix}play-{card.InstanceId}{ReturnGearMarker}{gear.InstanceId}-to-bf-{battlefield.Index}",
                        RiftboundActionType.PlayCard,
                        player.PlayerIndex,
                        $"Play {card.Name} to battlefield {battlefield.Name} [return {gear.Name}]"
                    )
                );
            }
        }

        return true;
    }

    public override void OnUnitPlay(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        string actionId
    )
    {
        var gearId = ParseChosenGearId(actionId);
        if (!gearId.HasValue)
        {
            return;
        }

        var gear = RiftboundEffectGearTargeting.EnumerateControlledGear(session, player.PlayerIndex)
            .FirstOrDefault(x => x.InstanceId == gearId.Value);
        if (gear is null || !RiftboundEffectGearTargeting.RemoveGearFromBoard(session, gear))
        {
            return;
        }

        gear.AttachedToInstanceId = null;
        session.Players[gear.OwnerPlayerIndex].HandZone.Cards.Add(gear);
        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "WhenPlay",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["returnedGear"] = gear.Name,
            }
        );
    }

    private static Guid? ParseChosenGearId(string actionId)
    {
        var markerIndex = actionId.IndexOf(ReturnGearMarker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return null;
        }

        var fragment = actionId[(markerIndex + ReturnGearMarker.Length)..];
        var match = GuidRegex().Match(fragment);
        if (!match.Success || !Guid.TryParse(match.Value, out var id))
        {
            return null;
        }

        return id;
    }
}
