using System.Globalization;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class DariusExecutionerEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "darius-executioner";
    public override string TemplateId => "named.darius-executioner";

    public override void OnUnitPlay(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        string actionId
    )
    {
        if (HasLegionEnabled(session, player.PlayerIndex))
        {
            card.IsExhausted = false;
        }
    }

    public override int GetBattlefieldUnitMightModifier(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        BattlefieldState battlefield,
        CardInstance unit
    )
    {
        if (
            unit.ControllerPlayerIndex != player.PlayerIndex
            || unit.InstanceId == card.InstanceId
            || !battlefield.Units.Any(x => x.InstanceId == card.InstanceId)
        )
        {
            return 0;
        }

        return 1;
    }

    private static bool HasLegionEnabled(GameSession session, int playerIndex)
    {
        return session.EffectContexts.Count(x =>
            x.ControllerPlayerIndex == playerIndex
            && string.Equals(x.Timing, "Play", StringComparison.OrdinalIgnoreCase)
            && x.Metadata.TryGetValue("turn", out var turn)
            && int.TryParse(turn, NumberStyles.Integer, CultureInfo.InvariantCulture, out var playedTurn)
            && playedTurn == session.TurnNumber) >= 2;
    }
}
