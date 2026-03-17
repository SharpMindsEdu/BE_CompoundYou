using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class AzirEmperorOfTheSandsEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "azir-emperor-of-the-sands";
    public override string TemplateId => "named.azir-emperor-of-the-sands";
    public override bool HasActivatedAbility => true;

    public override bool TryActivateAbility(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card
    )
    {
        if (card.IsExhausted || !HasPlayedEquipmentThisTurn(session, player.PlayerIndex))
        {
            return false;
        }

        if (!runtime.TryPayCost(session, player, energyCost: 1))
        {
            return false;
        }

        card.IsExhausted = true;
        ApplyWeaponmasterToSandSoldiers(session, player.PlayerIndex);
        var token = RiftboundTokenFactory.CreateSandSoldierUnitToken(
            ownerPlayerIndex: player.PlayerIndex,
            controllerPlayerIndex: player.PlayerIndex,
            might: 2,
            exhausted: true,
            grantWeaponmaster: true
        );
        player.BaseZone.Cards.Add(token);

        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "Activate",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["createdToken"] = token.Name,
                ["tokenMight"] = "2",
            }
        );
        return true;
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
        ApplyWeaponmasterToSandSoldiers(session, player.PlayerIndex);
    }

    private static bool HasPlayedEquipmentThisTurn(GameSession session, int playerIndex)
    {
        return session.EffectContexts.Any(context =>
            context.ControllerPlayerIndex == playerIndex
            && string.Equals(context.Timing, "Play", StringComparison.OrdinalIgnoreCase)
            && context.Metadata.TryGetValue("turn", out var turnText)
            && int.TryParse(turnText, out var turn)
            && turn == session.TurnNumber
            && context.Metadata.TryGetValue("template", out var template)
            && (
                string.Equals(template, "gear.attach-friendly-unit", StringComparison.OrdinalIgnoreCase)
                || string.Equals(template, "named.last-rites", StringComparison.OrdinalIgnoreCase)
            )
        );
    }

    private static void ApplyWeaponmasterToSandSoldiers(GameSession session, int playerIndex)
    {
        foreach (var unit in RiftboundEffectUnitTargeting.EnumerateFriendlyUnits(session, playerIndex))
        {
            if (!string.Equals(unit.Name, "Sand Soldier Token", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!unit.Keywords.Contains("Weaponmaster", StringComparer.OrdinalIgnoreCase))
            {
                unit.Keywords.Add("Weaponmaster");
            }
        }
    }
}
