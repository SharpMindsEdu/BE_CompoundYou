using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class HallowedTombEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "hallowed-tomb";
    public override string TemplateId => "named.hallowed-tomb";

    public override void OnHoldScore(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        BattlefieldState battlefield
    )
    {
        if (player.ChampionZone.Cards.Count > 0)
        {
            return;
        }

        var chosenChampion = player.TrashZone.Cards.FirstOrDefault(x =>
            x.EffectData.TryGetValue("isChosenChampion", out var value)
            && bool.TryParse(value, out var isChosenChampion)
            && isChosenChampion
        );
        if (chosenChampion is null)
        {
            return;
        }

        player.TrashZone.Cards.Remove(chosenChampion);
        player.ChampionZone.Cards.Add(chosenChampion);
        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "WhenHold",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["battlefield"] = battlefield.Name,
                ["returnedChampion"] = chosenChampion.Name,
            }
        );
    }
}

