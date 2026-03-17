using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class BreakneckMechEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "breakneck-mech";
    public override string TemplateId => "named.breakneck-mech";

    public override void OnUnitPlay(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        string actionId
    )
    {
        var hasOtherMech = RiftboundEffectUnitTargeting.EnumerateFriendlyUnits(session, player.PlayerIndex).Any(
            x => x.InstanceId != card.InstanceId && IsMech(x)
        );
        if (!hasOtherMech)
        {
            return;
        }

        card.IsExhausted = false;
        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "WhenPlay",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["enteredReady"] = "true",
            }
        );
    }

    public override bool GrantsKeywordToFriendlyUnit(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        CardInstance targetUnit,
        string keyword
    )
    {
        if (targetUnit.ControllerPlayerIndex != player.PlayerIndex || !IsMech(targetUnit))
        {
            return false;
        }

        return string.Equals(keyword, "Deflect", StringComparison.OrdinalIgnoreCase)
            || string.Equals(keyword, "Ganking", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMech(CardInstance unit)
    {
        return unit.Keywords.Contains("Mech", StringComparer.OrdinalIgnoreCase);
    }
}
