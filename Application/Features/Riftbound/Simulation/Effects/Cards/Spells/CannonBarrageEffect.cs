using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class CannonBarrageEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "cannon-barrage";
    public override string TemplateId => "named.cannon-barrage";

    public override bool TryAddLegalActions(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        List<RiftboundLegalAction> actions
    )
    {
        actions.Add(
            new RiftboundLegalAction(
                $"{runtime.ActionPrefix}play-{card.InstanceId}-spell",
                RiftboundActionType.PlayCard,
                player.PlayerIndex,
                $"Play spell {card.Name}"
            )
        );
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
        var magnitude = runtime.ReadMagnitude(card, fallback: 2)
            + runtime.GetSpellAndAbilityBonusDamage(session, player.PlayerIndex);
        var enemiesInCombat = session.Battlefields
            .SelectMany(x => x.Units)
            .Where(x =>
                x.ControllerPlayerIndex != player.PlayerIndex
                && (
                    x.Keywords.Contains("Attacker", StringComparer.OrdinalIgnoreCase)
                    || x.Keywords.Contains("Defender", StringComparer.OrdinalIgnoreCase)
                )
            )
            .ToList();
        if (enemiesInCombat.Count == 0)
        {
            return;
        }

        foreach (var enemy in enemiesInCombat)
        {
            enemy.MarkedDamage += magnitude;
        }

        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "Resolve",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["targets"] = enemiesInCombat.Count.ToString(),
                ["magnitude"] = magnitude.ToString(),
            }
        );
    }
}
