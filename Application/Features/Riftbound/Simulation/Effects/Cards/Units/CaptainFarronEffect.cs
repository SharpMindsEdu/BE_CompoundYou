using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class CaptainFarronEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "captain-farron";
    public override string TemplateId => "named.captain-farron";

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
        var buffed = 0;
        foreach (var friendly in battlefield.Units.Where(x =>
                     x.ControllerPlayerIndex == player.PlayerIndex
                     && x.InstanceId != card.InstanceId
                     && x.Keywords.Contains("Attacker", StringComparer.OrdinalIgnoreCase)))
        {
            friendly.TemporaryMightModifier += 1;
            buffed += 1;
        }

        if (buffed <= 0)
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
                ["buffedAttackers"] = buffed.ToString(),
            }
        );
    }
}
