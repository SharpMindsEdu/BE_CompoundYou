using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class GarenRuggedEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "garen-rugged";
    public override string TemplateId => "named.garen-rugged";

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

        card.TemporaryMightModifier += 2;
    }
}

