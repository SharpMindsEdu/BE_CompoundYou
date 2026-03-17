using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class ChemtechEnforcerEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "chemtech-enforcer";
    public override string TemplateId => "named.chemtech-enforcer";

    public override void OnUnitPlay(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        string actionId
    )
    {
        var discarded = player.HandZone.Cards
            .OrderBy(x => x.Cost.GetValueOrDefault())
            .ThenBy(x => x.Name, StringComparer.Ordinal)
            .ThenBy(x => x.InstanceId)
            .FirstOrDefault();
        if (discarded is null)
        {
            return;
        }

        runtime.DiscardFromHand(session, player, discarded, "chemtech-enforcer-play", card);
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
        if (!isAttacker)
        {
            return;
        }

        card.TemporaryMightModifier += 2;
    }
}

