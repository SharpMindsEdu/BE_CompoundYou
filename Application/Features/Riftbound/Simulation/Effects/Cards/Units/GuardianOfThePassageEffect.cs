using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class GuardianOfThePassageEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "guardian-of-the-passage";
    public override string TemplateId => "named.guardian-of-the-passage";

    public override void OnHoldScore(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        BattlefieldState battlefield
    )
    {
        var candidate = player.TrashZone.Cards
            .Where(x =>
                string.Equals(x.Type, "Unit", StringComparison.OrdinalIgnoreCase)
                || string.Equals(x.Type, "Gear", StringComparison.OrdinalIgnoreCase)
            )
            .OrderByDescending(x => x.Cost.GetValueOrDefault())
            .ThenBy(x => x.Name, StringComparer.Ordinal)
            .ThenBy(x => x.InstanceId)
            .FirstOrDefault();
        if (candidate is null)
        {
            return;
        }

        player.TrashZone.Cards.Remove(candidate);
        player.HandZone.Cards.Add(candidate);
        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "WhenHold",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["battlefield"] = battlefield.Name,
                ["returnedCard"] = candidate.Name,
            }
        );
    }
}

