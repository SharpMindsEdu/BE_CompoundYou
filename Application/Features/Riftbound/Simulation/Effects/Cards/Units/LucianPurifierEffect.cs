using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class LucianPurifierEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "lucian-purifier";
    public override string TemplateId => "named.lucian-purifier";

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
        var buffedAttackers = 0;
        foreach (
            var unit in battlefield.Units.Where(x =>
                x.ControllerPlayerIndex == player.PlayerIndex
                && x.Keywords.Contains("Attacker", StringComparer.OrdinalIgnoreCase)
            )
        )
        {
            var attachedEquipmentCount = CountAttachedEquipment(session, player.PlayerIndex, unit.InstanceId);
            if (attachedEquipmentCount <= 0)
            {
                continue;
            }

            unit.TemporaryMightModifier += attachedEquipmentCount;
            buffedAttackers += 1;
        }

        if (buffedAttackers <= 0)
        {
            return;
        }

        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "ShowdownStart",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["buffedAttackers"] = buffedAttackers.ToString(),
            }
        );
    }

    private static int CountAttachedEquipment(GameSession session, int playerIndex, Guid unitInstanceId)
    {
        return RiftboundEffectGearTargeting.EnumerateAllGear(session).Count(gear =>
            gear.ControllerPlayerIndex == playerIndex
            && gear.AttachedToInstanceId == unitInstanceId
            && IsEquipment(gear)
        );
    }

    private static bool IsEquipment(CardInstance gear)
    {
        return gear.Keywords.Contains("Equip", StringComparer.OrdinalIgnoreCase)
            || gear.Keywords.Contains("Equipment", StringComparer.OrdinalIgnoreCase);
    }
}
