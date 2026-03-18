using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class FioraPeerlessEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "fiora-peerless";
    public override string TemplateId => "named.fiora-peerless";

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

        var friendlyCount = battlefield.Units.Count(x => x.ControllerPlayerIndex == player.PlayerIndex);
        var enemyCount = battlefield.Units.Count(x => x.ControllerPlayerIndex != player.PlayerIndex);
        if (friendlyCount != 1 || enemyCount != 1)
        {
            return;
        }

        var currentMight = Math.Max(0, runtime.GetEffectiveMight(session, card));
        if (currentMight <= 0)
        {
            return;
        }

        card.TemporaryMightModifier += currentMight;
        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "ShowdownStart",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["oneOnOne"] = "true",
                ["addedMight"] = currentMight.ToString(),
            }
        );
    }
}
