using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class FioraWorthyEffect : RiftboundNamedCardEffectBase
{
    private const string WasMightyKey = "fioraWorthy.wasMighty";

    public override string NameIdentifier => "fiora-worthy";
    public override string TemplateId => "named.fiora-worthy";

    public override void OnFriendlyCardPlayed(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        CardInstance playedCard,
        string actionId
    )
    {
        var becameMighty = new List<CardInstance>();
        foreach (var unit in RiftboundEffectUnitTargeting.EnumerateFriendlyUnits(session, player.PlayerIndex))
        {
            var isMighty = runtime.GetEffectiveMight(session, unit) >= 5;
            var wasMighty = unit.EffectData.TryGetValue(WasMightyKey, out var rawWasMighty)
                && bool.TryParse(rawWasMighty, out var parsedWasMighty)
                && parsedWasMighty;
            if (isMighty && !wasMighty)
            {
                becameMighty.Add(unit);
            }

            unit.EffectData[WasMightyKey] = isMighty ? "true" : "false";
        }

        var toReady = becameMighty.Where(x => x.IsExhausted)
            .OrderByDescending(x => runtime.GetEffectiveMight(session, x))
            .ThenBy(x => x.Name, StringComparer.Ordinal)
            .ThenBy(x => x.InstanceId)
            .FirstOrDefault();
        if (toReady is null)
        {
            return;
        }

        if (
            !runtime.TryPayCost(
                session,
                player,
                energyCost: 0,
                [new EffectPowerRequirement(1, ["Order"])]
            )
        )
        {
            return;
        }

        toReady.IsExhausted = false;
        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "WhenMighty",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["target"] = toReady.Name,
                ["paidOrder"] = "true",
            }
        );
    }
}
