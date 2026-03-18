using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class FortifiedPositionEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "fortified-position";
    public override string TemplateId => "named.fortified-position";

    public override void OnBattlefieldShowdownStart(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        CardInstance card,
        BattlefieldState battlefield,
        int attackerPlayerIndex,
        int defenderPlayerIndex
    )
    {
        var target = battlefield.Units
            .Where(x => x.ControllerPlayerIndex == defenderPlayerIndex)
            .OrderByDescending(x => runtime.GetEffectiveMight(session, x))
            .ThenBy(x => x.Name, StringComparer.Ordinal)
            .ThenBy(x => x.InstanceId)
            .FirstOrDefault();
        if (target is null)
        {
            return;
        }

        target.ShieldCount += 2;
        runtime.AddEffectContext(
            session,
            card.Name,
            defenderPlayerIndex,
            "ShowdownStart",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["target"] = target.Name,
                ["shield"] = "2",
            }
        );
    }
}
