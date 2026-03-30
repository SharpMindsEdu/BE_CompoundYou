using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class LeonaZealotEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "leona-zealot";
    public override string TemplateId => "named.leona-zealot";

    public override void OnUnitPlay(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        string actionId
    )
    {
        var opponentNearVictory = session.Players.Any(x =>
            x.PlayerIndex != player.PlayerIndex && x.Score >= 5
        );
        if (!opponentNearVictory)
        {
            return;
        }

        card.IsExhausted = false;
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
            !battlefield.Units.Any(x => x.InstanceId == card.InstanceId)
            || unit.ControllerPlayerIndex == player.PlayerIndex
            || !unit.EffectData.TryGetValue("stunnedThisTurn", out var stunnedText)
            || !bool.TryParse(stunnedText, out var stunned)
            || !stunned
        )
        {
            return 0;
        }

        var currentMight = unit.Might.GetValueOrDefault()
            + unit.PermanentMightModifier
            + unit.TemporaryMightModifier;
        if (currentMight <= 1)
        {
            return 0;
        }

        var reduction = Math.Min(8, currentMight - 1);
        return -reduction;
    }
}
