using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class AkshanMischievousEffect : RiftboundNamedCardEffectBase
{
    private const string AdditionalCostActionMarker = "-akshan-additional-cost-";

    public override string NameIdentifier => "akshan-mischievous";
    public override string TemplateId => "named.akshan-mischievous";

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

        var opponentIndex = session.Players.Select(x => x.PlayerIndex).FirstOrDefault(x => x != player.PlayerIndex);
        var enemyGear = RiftboundEffectGearTargeting.EnumerateControlledGear(session, opponentIndex);
        foreach (var gear in enemyGear)
        {
            foreach (var destination in destinations)
            {
                actions.Add(
                    new RiftboundLegalAction(
                        $"{runtime.ActionPrefix}play-{card.InstanceId}{AdditionalCostActionMarker}target-gear-{gear.InstanceId}{destination.Suffix}",
                        RiftboundActionType.PlayCard,
                        player.PlayerIndex,
                        $"{destination.Description} (+[Body][Body], steal {gear.Name})"
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
        var paidAdditionalCost = actionId.Contains(AdditionalCostActionMarker, StringComparison.Ordinal);
        if (!paidAdditionalCost)
        {
            return;
        }

        var selectedGear = RiftboundEffectGearTargeting.ResolveTargetGearFromAction(session, actionId);
        if (selectedGear is null || selectedGear.ControllerPlayerIndex == player.PlayerIndex)
        {
            return;
        }

        var previousController = selectedGear.ControllerPlayerIndex;
        if (!RiftboundEffectGearTargeting.RemoveGearFromBoard(session, selectedGear))
        {
            return;
        }

        selectedGear.AttachedToInstanceId = null;
        selectedGear.ControllerPlayerIndex = player.PlayerIndex;
        AddGearToAkshanLocation(session, player, card, selectedGear);
        var attached = false;
        if (IsEquipment(selectedGear))
        {
            selectedGear.AttachedToInstanceId = card.InstanceId;
            attached = true;
        }

        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "WhenPlay",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["paidAdditionalCost"] = "true",
                ["stolenGear"] = selectedGear.Name,
                ["fromController"] = previousController.ToString(),
                ["attachedToAkshan"] = attached ? "true" : "false",
            }
        );
    }

    private static bool IsEquipment(CardInstance gear)
    {
        return gear.Keywords.Contains("Equip", StringComparer.OrdinalIgnoreCase)
            || string.Equals(gear.EffectTemplateId, "gear.attach-friendly-unit", StringComparison.Ordinal);
    }

    private static void AddGearToAkshanLocation(
        GameSession session,
        PlayerState player,
        CardInstance akshan,
        CardInstance gear
    )
    {
        var battlefield = RiftboundEffectUnitTargeting.FindBattlefieldContainingUnit(session, akshan.InstanceId);
        if (battlefield is not null)
        {
            battlefield.Gear.Add(gear);
            return;
        }

        player.BaseZone.Cards.Add(gear);
    }
}
