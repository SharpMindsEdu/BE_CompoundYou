using System.Globalization;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class LucianMercilessEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "lucian-merciless";
    public override string TemplateId => "named.lucian-merciless";

    public override void OnConquer(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        BattlefieldState battlefield,
        string? sourceActionId
    )
    {
        var alreadyTriggeredThisTurn = session.EffectContexts.Any(context =>
            context.ControllerPlayerIndex == player.PlayerIndex
            && string.Equals(context.Source, card.Name, StringComparison.OrdinalIgnoreCase)
            && string.Equals(context.Timing, "WhenConquer", StringComparison.OrdinalIgnoreCase)
            && context.Metadata.TryGetValue("instanceId", out var instanceId)
            && string.Equals(instanceId, card.InstanceId.ToString(), StringComparison.OrdinalIgnoreCase)
            && context.Metadata.TryGetValue("turn", out var turnText)
            && int.TryParse(turnText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var turn)
            && turn == session.TurnNumber
        );
        if (alreadyTriggeredThisTurn)
        {
            return;
        }

        card.IsExhausted = false;
        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "WhenConquer",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["instanceId"] = card.InstanceId.ToString(),
                ["turn"] = session.TurnNumber.ToString(CultureInfo.InvariantCulture),
                ["ready"] = "true",
                ["battlefield"] = battlefield.Name,
            }
        );
    }
}
