using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class DrMundoExpertEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "dr-mundo-expert";
    public override string TemplateId => "named.dr-mundo-expert";

    public override int GetBattlefieldUnitMightModifier(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        BattlefieldState battlefield,
        CardInstance unit
    )
    {
        if (unit.InstanceId != card.InstanceId)
        {
            return 0;
        }

        return player.TrashZone.Cards.Count;
    }
}

