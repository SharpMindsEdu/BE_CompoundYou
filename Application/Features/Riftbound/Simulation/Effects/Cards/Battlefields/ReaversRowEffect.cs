using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class ReaversRowEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "reaver-s-row";
    public override string TemplateId => "named.reaver-s-row";

    public override void OnBattlefieldShowdownStart(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        CardInstance card,
        BattlefieldState battlefield,
        int attackerPlayerIndex,
        int defenderPlayerIndex
    )
    {
        var defender = session.Players.FirstOrDefault(x => x.PlayerIndex == defenderPlayerIndex);
        if (defender is null)
        {
            return;
        }

        var movedUnit = battlefield.Units
            .Where(x => x.ControllerPlayerIndex == defenderPlayerIndex)
            .OrderBy(x => runtime.GetEffectiveMight(session, x))
            .ThenBy(x => x.Name, StringComparer.Ordinal)
            .ThenBy(x => x.InstanceId)
            .FirstOrDefault();
        if (movedUnit is not null)
        {
            battlefield.Units.Remove(movedUnit);
            defender.BaseZone.Cards.Add(movedUnit);
        }

        runtime.AddEffectContext(
            session,
            card.Name,
            defenderPlayerIndex,
            "WhenDefend",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["battlefield"] = battlefield.Name,
                ["movedUnit"] = movedUnit?.Name ?? string.Empty,
                ["moved"] = (movedUnit is not null).ToString().ToLowerInvariant(),
            }
        );
    }
}

