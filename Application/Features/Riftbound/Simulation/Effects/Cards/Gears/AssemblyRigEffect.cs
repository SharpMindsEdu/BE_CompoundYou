using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class AssemblyRigEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "assembly-rig";
    public override string TemplateId => "named.assembly-rig";
    public override bool HasActivatedAbility => true;

    public override bool TryActivateAbility(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card
    )
    {
        if (card.IsExhausted)
        {
            return false;
        }

        var recycledUnit = player.TrashZone.Cards
            .Where(x => string.Equals(x.Type, "Unit", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.Might.GetValueOrDefault())
            .ThenByDescending(x => x.Cost.GetValueOrDefault())
            .ThenBy(x => x.Name, StringComparer.Ordinal)
            .ThenBy(x => x.InstanceId)
            .FirstOrDefault();
        if (recycledUnit is null)
        {
            return false;
        }

        if (
            !runtime.TryPayCost(
                session,
                player,
                energyCost: 1,
                [new EffectPowerRequirement(1, ["Fury"])]
            )
        )
        {
            return false;
        }

        card.IsExhausted = true;
        player.TrashZone.Cards.Remove(recycledUnit);
        player.MainDeckZone.Cards.Add(recycledUnit);
        var mechToken = RiftboundTokenFactory.CreateMechUnitToken(
            ownerPlayerIndex: player.PlayerIndex,
            controllerPlayerIndex: player.PlayerIndex,
            might: 3,
            exhausted: true
        );
        player.BaseZone.Cards.Add(mechToken);

        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "Activate",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["recycledUnit"] = recycledUnit.Name,
                ["createdToken"] = mechToken.Name,
            }
        );

        return true;
    }
}

