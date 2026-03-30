using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class KrakenHunterEffect : RiftboundNamedCardEffectBase
{
    public const string SpendBuffMarker = "-kraken-hunter-spend-buffs-";

    public override string NameIdentifier => "kraken-hunter";
    public override string TemplateId => "named.kraken-hunter";

    public override bool TryAddUnitPlayLegalActions(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        List<RiftboundLegalAction> actions
    )
    {
        var destinations = new List<(string Suffix, string Description)>
        {
            ("-to-base", $"Play {card.Name} to base"),
        };
        destinations.AddRange(
            session.Battlefields
                .Where(x => x.ControlledByPlayerIndex == player.PlayerIndex)
                .OrderBy(x => x.Index)
                .Select(x => ($"-to-bf-{x.Index}", $"Play {card.Name} to battlefield {x.Name}"))
        );

        foreach (var destination in destinations)
        {
            actions.Add(
                new RiftboundLegalAction(
                    $"{runtime.ActionPrefix}play-{card.InstanceId}{destination.Suffix}",
                    RiftboundActionType.PlayCard,
                    player.PlayerIndex,
                    destination.Description
                )
            );
        }

        var maxSpendableBuffs = ResolveSpendableBuffCount(session, player.PlayerIndex);
        for (var buffsToSpend = 1; buffsToSpend <= maxSpendableBuffs; buffsToSpend += 1)
        {
            foreach (var destination in destinations)
            {
                actions.Add(
                    new RiftboundLegalAction(
                        $"{runtime.ActionPrefix}play-{card.InstanceId}{SpendBuffMarker}{buffsToSpend}{destination.Suffix}",
                        RiftboundActionType.PlayCard,
                        player.PlayerIndex,
                        $"{destination.Description} (spend {buffsToSpend} buff{(buffsToSpend == 1 ? string.Empty : "s")})"
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
        if (!TryParseSpentBuffCount(actionId, out var buffsToSpend) || buffsToSpend <= 0)
        {
            return;
        }

        var spent = SpendBuffs(session, player.PlayerIndex, buffsToSpend);
        if (spent <= 0)
        {
            return;
        }

        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "WhenPlay",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["spentBuffs"] = spent.ToString(),
                ["reducedByBody"] = spent.ToString(),
            }
        );
    }

    public override void OnShowdownStart(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        BattlefieldState battlefield,
        bool isAttacker,
        bool isDefender
    )
    {
        if (!isAttacker)
        {
            return;
        }

        card.TemporaryMightModifier += 1;
    }

    public static bool TryParseSpentBuffCount(string actionId, out int spentBuffs)
    {
        spentBuffs = 0;
        var markerIndex = actionId.IndexOf(SpendBuffMarker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return false;
        }

        var remainder = actionId[(markerIndex + SpendBuffMarker.Length)..];
        var digitsLength = 0;
        while (digitsLength < remainder.Length && char.IsDigit(remainder[digitsLength]))
        {
            digitsLength += 1;
        }

        if (digitsLength == 0)
        {
            return false;
        }

        return int.TryParse(remainder[..digitsLength], out spentBuffs);
    }

    private static int ResolveSpendableBuffCount(GameSession session, int playerIndex)
    {
        return RiftboundEffectUnitTargeting
            .EnumerateFriendlyUnits(session, playerIndex)
            .Sum(x => Math.Max(0, x.PermanentMightModifier));
    }

    private static int SpendBuffs(GameSession session, int playerIndex, int targetSpend)
    {
        var spent = 0;
        var ordered = RiftboundEffectUnitTargeting
            .EnumerateFriendlyUnits(session, playerIndex)
            .Where(x => x.PermanentMightModifier > 0)
            .OrderByDescending(x => x.PermanentMightModifier)
            .ThenBy(x => x.Name, StringComparer.Ordinal)
            .ThenBy(x => x.InstanceId)
            .ToList();
        foreach (var unit in ordered)
        {
            while (unit.PermanentMightModifier > 0 && spent < targetSpend)
            {
                unit.PermanentMightModifier -= 1;
                spent += 1;
            }

            if (spent >= targetSpend)
            {
                return spent;
            }
        }

        return spent;
    }
}
