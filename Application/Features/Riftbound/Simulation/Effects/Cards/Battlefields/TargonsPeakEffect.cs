using System.Globalization;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class TargonsPeakEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "targon-s-peak";
    public override string TemplateId => "named.targon-s-peak";

    public override void OnConquer(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        BattlefieldState battlefield,
        string? sourceActionId
    )
    {
        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "WhenConquer",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["battlefield"] = battlefield.Name,
                ["turn"] = session.TurnNumber.ToString(CultureInfo.InvariantCulture),
                ["readyRunesEndTurn"] = "2",
                ["battlefieldCardId"] = battlefield.CardId.ToString(CultureInfo.InvariantCulture),
            }
        );
    }

    public override void OnEndTurn(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card
    )
    {
        var battlefieldCardIdText = card.CardId.ToString(CultureInfo.InvariantCulture);
        var totalToReady = session.EffectContexts
            .Where(context =>
                context.ControllerPlayerIndex == player.PlayerIndex
                && string.Equals(context.Source, card.Name, StringComparison.OrdinalIgnoreCase)
                && string.Equals(context.Timing, "WhenConquer", StringComparison.OrdinalIgnoreCase)
                && context.Metadata.TryGetValue("turn", out var turnText)
                && int.TryParse(
                    turnText,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var turn
                )
                && turn == session.TurnNumber
                && context.Metadata.TryGetValue("battlefieldCardId", out var cardIdText)
                && string.Equals(
                    cardIdText,
                    battlefieldCardIdText,
                    StringComparison.Ordinal
                )
            )
            .Select(context =>
                context.Metadata.TryGetValue("readyRunesEndTurn", out var value)
                && int.TryParse(
                    value,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var amount
                )
                    ? Math.Max(0, amount)
                    : 0
            )
            .Sum();
        if (totalToReady <= 0)
        {
            return;
        }

        var readied = 0;
        foreach (
            var rune in player.BaseZone.Cards
                .Where(cardInstance =>
                    string.Equals(cardInstance.Type, "Rune", StringComparison.OrdinalIgnoreCase)
                    && cardInstance.IsExhausted
                )
                .Take(totalToReady)
        )
        {
            rune.IsExhausted = false;
            readied += 1;
        }

        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "EndTurn",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["readiedRunes"] = readied.ToString(CultureInfo.InvariantCulture),
            }
        );
    }
}
