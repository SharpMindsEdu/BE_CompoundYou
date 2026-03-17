using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class BackAlleyBarEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "back-alley-bar";
    public override string TemplateId => "named.back-alley-bar";

    public override void OnUnitMoveFromBattlefield(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        BattlefieldState battlefield,
        CardInstance movedUnit
    )
    {
        movedUnit.TemporaryMightModifier += 1;
        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "WhenMoveFrom",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["battlefield"] = battlefield.Name,
                ["unit"] = movedUnit.Name,
                ["magnitude"] = "1",
            }
        );
    }
}

