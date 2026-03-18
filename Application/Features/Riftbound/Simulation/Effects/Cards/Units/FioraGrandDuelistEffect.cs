using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class FioraGrandDuelistEffect : RiftboundNamedCardEffectBase
{
    private const string WasMightyKey = "fioraGrandDuelist.wasMighty";

    public override string NameIdentifier => "fiora-grand-duelist";
    public override string TemplateId => "named.fiora-grand-duelist";

    public override void OnFriendlyCardPlayed(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        CardInstance playedCard,
        string actionId
    )
    {
        var becameMighty = 0;
        foreach (var unit in RiftboundEffectUnitTargeting.EnumerateFriendlyUnits(session, player.PlayerIndex))
        {
            var isMighty = runtime.GetEffectiveMight(session, unit) >= 5;
            var wasMighty = unit.EffectData.TryGetValue(WasMightyKey, out var rawWasMighty)
                && bool.TryParse(rawWasMighty, out var parsedWasMighty)
                && parsedWasMighty;
            if (isMighty && !wasMighty)
            {
                becameMighty += 1;
            }

            unit.EffectData[WasMightyKey] = isMighty ? "true" : "false";
        }

        if (becameMighty <= 0 || card.IsExhausted)
        {
            return;
        }

        card.IsExhausted = true;
        var channeled = 0;
        if (player.RuneDeckZone.Cards.Count > 0)
        {
            var rune = player.RuneDeckZone.Cards[0];
            player.RuneDeckZone.Cards.RemoveAt(0);
            rune.IsExhausted = true;
            player.BaseZone.Cards.Add(rune);
            channeled = 1;
        }

        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "WhenMighty",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["becameMighty"] = becameMighty.ToString(),
                ["channeledRunes"] = channeled.ToString(),
            }
        );
    }
}
