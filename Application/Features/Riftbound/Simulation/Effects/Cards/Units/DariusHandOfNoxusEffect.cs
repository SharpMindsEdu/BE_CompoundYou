using System.Globalization;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class DariusHandOfNoxusEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "darius-hand-of-noxus";
    public override string TemplateId => "named.darius-hand-of-noxus";
    public override bool HasActivatedAbility => true;

    public override bool TryActivateAbility(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card
    )
    {
        if (card.IsExhausted || !HasLegionEnabled(session, player.PlayerIndex))
        {
            return false;
        }

        card.IsExhausted = true;
        player.RunePool.Energy += 1;
        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "Activate",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["addedEnergy"] = "1",
            }
        );
        return true;
    }

    private static bool HasLegionEnabled(GameSession session, int playerIndex)
    {
        return session.EffectContexts.Any(x =>
            x.ControllerPlayerIndex == playerIndex
            && string.Equals(x.Timing, "Play", StringComparison.OrdinalIgnoreCase)
            && x.Metadata.TryGetValue("turn", out var turnText)
            && int.TryParse(turnText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var turn)
            && turn == session.TurnNumber);
    }
}

