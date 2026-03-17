using System.Globalization;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class DravenAudaciousEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "draven-audacious";
    public override string TemplateId => "named.draven-audacious";

    public override void OnWinCombat(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        BattlefieldState battlefield
    )
    {
        var alreadyTriggeredThisTurn = session.EffectContexts.Any(context =>
            context.ControllerPlayerIndex == player.PlayerIndex
            && string.Equals(context.Source, card.Name, StringComparison.OrdinalIgnoreCase)
            && string.Equals(context.Timing, "WhenWinCombat", StringComparison.OrdinalIgnoreCase)
            && context.Metadata.TryGetValue("turn", out var turnText)
            && int.TryParse(turnText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var turn)
            && turn == session.TurnNumber);
        if (alreadyTriggeredThisTurn)
        {
            return;
        }

        player.Score += 1;
        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "WhenWinCombat",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["scored"] = "1",
                ["battlefield"] = battlefield.Name,
            }
        );
    }
}

