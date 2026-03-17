using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class DravenVanquisherEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "draven-vanquisher";
    public override string TemplateId => "named.draven-vanquisher";

    public override void OnWinCombat(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        BattlefieldState battlefield
    )
    {
        player.BaseZone.Cards.Add(
            RiftboundTokenFactory.CreateGoldGearToken(
                ownerPlayerIndex: player.PlayerIndex,
                controllerPlayerIndex: player.PlayerIndex,
                exhausted: true
            )
        );
    }

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
        if (!isAttacker && !isDefender)
        {
            return;
        }

        if (
            !runtime.TryPayCost(
                session,
                player,
                energyCost: 0,
                [new EffectPowerRequirement(1, ["Fury"])]
            )
        )
        {
            return;
        }

        card.TemporaryMightModifier += 2;
        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "ShowdownStart",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["paidFury"] = "true",
                ["buff"] = "2",
            }
        );
    }
}

