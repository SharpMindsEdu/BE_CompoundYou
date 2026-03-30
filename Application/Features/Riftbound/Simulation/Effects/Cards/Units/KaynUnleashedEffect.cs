using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class KaynUnleashedEffect : RiftboundNamedCardEffectBase
{
    private const string MoveCountKey = "kaynMovedThisTurn";
    private const string DamagePreventionKey = "preventNextDamageThisTurn";
    private const int FullTurnPreventionCharges = 999;

    public override string NameIdentifier => "kayn-unleashed";
    public override string TemplateId => "named.kayn-unleashed";

    public override void OnUnitMove(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card
    )
    {
        var moved = runtime.ReadIntEffectData(card, MoveCountKey, fallback: 0) + 1;
        card.EffectData[MoveCountKey] = moved.ToString();
        if (moved < 2)
        {
            return;
        }

        card.EffectData[DamagePreventionKey] = FullTurnPreventionCharges.ToString();
        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "WhenMove",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["movesThisTurn"] = moved.ToString(),
                ["ignoreDamageThisTurn"] = "true",
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
        card.EffectData.Remove(MoveCountKey);
    }
}
