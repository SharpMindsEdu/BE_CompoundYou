using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class KaiSaEvolutionaryEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "kai-sa-evolutionary";
    public override string TemplateId => "named.kai-sa-evolutionary";

    public override void OnConquer(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        BattlefieldState battlefield,
        string? sourceActionId
    )
    {
        var maxEnergyCost = player.Score - 1;
        if (maxEnergyCost <= 0)
        {
            return;
        }

        runtime.TryPlaySpellFromTrash(
            session,
            player,
            card,
            maxEnergyCost,
            ignoreEnergyCost: true,
            recycleAfterPlay: true,
            timing: "WhenConquer"
        );
    }
}
