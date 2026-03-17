using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class AriseEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "arise";
    public override string TemplateId => "named.arise";

    public override void OnSpellOrGearPlay(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        string actionId
    )
    {
        var equipmentCount = RiftboundEffectGearTargeting.EnumerateControlledGear(session, player.PlayerIndex)
            .Count(IsEquipment);
        if (equipmentCount <= 0)
        {
            return;
        }

        var created = new List<CardInstance>();
        var grantWeaponmaster = player.LegendZone.Cards.Any(x =>
            string.Equals(x.EffectTemplateId, "named.azir-emperor-of-the-sands", StringComparison.Ordinal)
        );
        for (var i = 0; i < equipmentCount; i += 1)
        {
            var token = RiftboundTokenFactory.CreateSandSoldierUnitToken(
                ownerPlayerIndex: player.PlayerIndex,
                controllerPlayerIndex: player.PlayerIndex,
                might: 2,
                exhausted: true,
                grantWeaponmaster
            );
            player.BaseZone.Cards.Add(token);
            created.Add(token);
        }

        var readied = 0;
        foreach (var soldier in created.Take(2))
        {
            soldier.IsExhausted = false;
            readied += 1;
        }

        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "Resolve",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["equipmentCount"] = equipmentCount.ToString(),
                ["createdSandSoldiers"] = created.Count.ToString(),
                ["readiedSandSoldiers"] = readied.ToString(),
            }
        );
    }

    private static bool IsEquipment(CardInstance gear)
    {
        return gear.Keywords.Contains("Equip", StringComparer.OrdinalIgnoreCase)
            || string.Equals(gear.EffectTemplateId, "gear.attach-friendly-unit", StringComparison.Ordinal)
            || string.Equals(gear.EffectTemplateId, "named.b-f-sword", StringComparison.Ordinal)
            || string.Equals(gear.EffectTemplateId, "named.last-rites", StringComparison.Ordinal);
    }
}
