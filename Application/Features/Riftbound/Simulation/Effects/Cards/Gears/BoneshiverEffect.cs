using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class BoneshiverEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "boneshiver";
    public override string TemplateId => "named.boneshiver";

    protected override IReadOnlyDictionary<string, string> BuildData(
        RiftboundCard card,
        string normalizedEffectText
    )
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["attachedMightBonus"] = "2",
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
        foreach (var target in RiftboundEffectUnitTargeting.EnumerateFriendlyUnits(session, player.PlayerIndex))
        {
            actions.Add(
                new RiftboundLegalAction(
                    $"{runtime.ActionPrefix}play-{card.InstanceId}-spell-target-unit-{target.InstanceId}",
                    RiftboundActionType.PlayCard,
                    player.PlayerIndex,
                    $"Play {card.Name} targeting {target.Name}"
                )
            );
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
        var target = RiftboundEffectUnitTargeting.ResolveTargetUnitFromAction(session, actionId);
        if (target is null || target.ControllerPlayerIndex != player.PlayerIndex)
        {
            return;
        }

        card.AttachedToInstanceId = target.InstanceId;
        card.IsExhausted = true;
        var battlefield = RiftboundEffectUnitTargeting.FindBattlefieldContainingUnit(session, target.InstanceId);
        if (battlefield is not null)
        {
            battlefield.Gear.Add(card);
        }
        else
        {
            player.BaseZone.Cards.Add(card);
        }

        runtime.NotifyGearAttached(session, card, target);
    }

    public override void OnConquer(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        BattlefieldState battlefield
    )
    {
        if (player.RuneDeckZone.Cards.Count == 0)
        {
            return;
        }

        var rune = player.RuneDeckZone.Cards[0];
        player.RuneDeckZone.Cards.RemoveAt(0);
        rune.IsExhausted = true;
        player.BaseZone.Cards.Add(rune);
        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "WhenConquer",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["channeledRunesExhausted"] = "1",
                ["battlefield"] = battlefield.Name,
            }
        );
    }
}
