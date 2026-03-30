using Application.Features.Riftbound.Simulation.Engine;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class MorbidReturnEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "morbid-return";
    public override string TemplateId => "named.morbid-return";

    public override bool TryAddLegalActions(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        List<RiftboundLegalAction> actions
    )
    {
        foreach (var target in player.TrashZone.Cards.Where(x => string.Equals(x.Type, "Unit", StringComparison.OrdinalIgnoreCase)))
        {
            actions.Add(
                new RiftboundLegalAction(
                    $"{runtime.ActionPrefix}play-{card.InstanceId}-spell-target-unit-{target.InstanceId}",
                    RiftboundActionType.PlayCard,
                    player.PlayerIndex,
                    $"Play {card.Name} returning {target.Name}"
                )
            );
        }

        return actions.Count > 0;
    }

    public override void OnSpellOrGearPlay(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        string actionId
    )
    {
        var target = player.TrashZone.Cards
            .Where(x => string.Equals(x.Type, "Unit", StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault(x => actionId.Contains(x.InstanceId.ToString(), StringComparison.Ordinal));
        if (target is null)
        {
            return;
        }

        player.TrashZone.Cards.Remove(target);
        player.HandZone.Cards.Add(target);

        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "Resolve",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["returned"] = target.Name,
            }
        );
    }
}
