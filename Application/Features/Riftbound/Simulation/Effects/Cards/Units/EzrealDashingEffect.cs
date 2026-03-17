using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class EzrealDashingEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "ezreal-dashing";
    public override string TemplateId => "named.ezreal-dashing";
    public override bool HasActivatedAbility => true;

    protected override IReadOnlyDictionary<string, string> BuildData(
        RiftboundCard card,
        string normalizedEffectText
    )
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["noCombatDamage"] = "true",
        };
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

        var enemy = battlefield.Units
            .Where(x => x.ControllerPlayerIndex != player.PlayerIndex)
            .OrderByDescending(x => runtime.GetEffectiveMight(session, x))
            .ThenBy(x => x.Name, StringComparer.Ordinal)
            .ThenBy(x => x.InstanceId)
            .FirstOrDefault();
        if (enemy is null)
        {
            return;
        }

        enemy.MarkedDamage += Math.Max(0, runtime.GetEffectiveMight(session, card));
    }

    public override bool TryActivateAbility(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card
    )
    {
        if (
            !runtime.TryPayCost(
                session,
                player,
                energyCost: 0,
                [new EffectPowerRequirement(1, ["Mind"])]
            )
        )
        {
            return false;
        }

        RemoveUnitFromCurrentLocation(session, card);
        player.BaseZone.Cards.Add(card);
        card.IsExhausted = true;
        return true;
    }

    private static void RemoveUnitFromCurrentLocation(GameSession session, CardInstance unit)
    {
        foreach (var player in session.Players)
        {
            if (player.BaseZone.Cards.Remove(unit))
            {
                return;
            }
        }

        foreach (var battlefield in session.Battlefields)
        {
            if (battlefield.Units.Remove(unit))
            {
                return;
            }
        }
    }
}

