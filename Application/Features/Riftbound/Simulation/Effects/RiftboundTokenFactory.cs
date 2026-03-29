using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

internal static class RiftboundTokenFactory
{
    public static CardInstance CreateGoldGearToken(
        int ownerPlayerIndex,
        int controllerPlayerIndex,
        bool exhausted = true
    )
    {
        return new CardInstance
        {
            InstanceId = Guid.NewGuid(),
            CardId = -10_001,
            Name = "Gold Token",
            Type = "Gear",
            OwnerPlayerIndex = ownerPlayerIndex,
            ControllerPlayerIndex = controllerPlayerIndex,
            Cost = 0,
            Power = 0,
            ColorDomains = [],
            Might = 0,
            IsToken = true,
            IsExhausted = exhausted,
            EffectTemplateId = "named.gold",
            EffectData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            Keywords = ["Reaction"],
        };
    }

    public static CardInstance CreateMechUnitToken(
        int ownerPlayerIndex,
        int controllerPlayerIndex,
        int might = 3,
        bool exhausted = true
    )
    {
        return new CardInstance
        {
            InstanceId = Guid.NewGuid(),
            CardId = -10_002,
            Name = "Mech Token",
            Type = "Unit",
            OwnerPlayerIndex = ownerPlayerIndex,
            ControllerPlayerIndex = controllerPlayerIndex,
            Cost = 0,
            Power = 0,
            ColorDomains = [],
            Might = might,
            IsToken = true,
            IsExhausted = exhausted,
            EffectTemplateId = "unit.vanilla",
            EffectData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            Keywords = [],
        };
    }

    public static CardInstance CreateRecruitUnitToken(
        int ownerPlayerIndex,
        int controllerPlayerIndex,
        int might = 1,
        bool exhausted = true
    )
    {
        return new CardInstance
        {
            InstanceId = Guid.NewGuid(),
            CardId = -10_003,
            Name = "Recruit Token",
            Type = "Unit",
            OwnerPlayerIndex = ownerPlayerIndex,
            ControllerPlayerIndex = controllerPlayerIndex,
            Cost = 0,
            Power = 0,
            ColorDomains = [],
            Might = might,
            IsToken = true,
            IsExhausted = exhausted,
            EffectTemplateId = "unit.vanilla",
            EffectData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            Keywords = [],
        };
    }

    public static CardInstance CreateSandSoldierUnitToken(
        int ownerPlayerIndex,
        int controllerPlayerIndex,
        int might = 2,
        bool exhausted = true,
        bool grantWeaponmaster = false
    )
    {
        var keywords = new List<string>();
        if (grantWeaponmaster)
        {
            keywords.Add("Weaponmaster");
        }

        return new CardInstance
        {
            InstanceId = Guid.NewGuid(),
            CardId = -10_004,
            Name = "Sand Soldier Token",
            Type = "Unit",
            OwnerPlayerIndex = ownerPlayerIndex,
            ControllerPlayerIndex = controllerPlayerIndex,
            Cost = 0,
            Power = 0,
            ColorDomains = [],
            Might = might,
            IsToken = true,
            IsExhausted = exhausted,
            EffectTemplateId = "unit.vanilla",
            EffectData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            Keywords = keywords,
        };
    }
}
