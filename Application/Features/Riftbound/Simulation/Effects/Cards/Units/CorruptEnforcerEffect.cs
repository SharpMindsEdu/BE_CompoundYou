using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class CorruptEnforcerEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "corrupt-enforcer";
    public override string TemplateId => "named.corrupt-enforcer";

    public override void OnUnitMove(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card
    )
    {
        if (RiftboundEffectUnitTargeting.FindBattlefieldContainingUnit(session, card.InstanceId) is null)
        {
            return;
        }

        var discarded = player.HandZone.Cards
            .OrderBy(x => x.Cost.GetValueOrDefault())
            .ThenBy(x => x.Name, StringComparer.Ordinal)
            .ThenBy(x => x.InstanceId)
            .FirstOrDefault();
        if (discarded is null)
        {
            return;
        }

        runtime.DiscardFromHand(session, player, discarded, "corrupt-enforcer-move", card);
    }

    public override void OnWinCombat(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        BattlefieldState battlefield
    )
    {
        runtime.DrawCards(player, 1);
    }
}
