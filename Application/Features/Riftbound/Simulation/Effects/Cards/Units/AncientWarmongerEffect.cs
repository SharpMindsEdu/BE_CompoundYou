using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class AncientWarmongerEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "ancient-warmonger";
    public override string TemplateId => "named.ancient-warmonger";

    public override void OnShowdownStart(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        BattlefieldState battlefield,
        bool isAttacker,
        bool isDefender
    )
    {
        if (!isAttacker)
        {
            return;
        }

        var enemyCount = battlefield.Units.Count(x => x.ControllerPlayerIndex != player.PlayerIndex);
        if (enemyCount <= 0)
        {
            return;
        }

        card.TemporaryMightModifier += enemyCount;
        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "WhenAttack",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["assaultInstances"] = enemyCount.ToString(),
                ["battlefield"] = battlefield.Name,
            }
        );
    }
}
