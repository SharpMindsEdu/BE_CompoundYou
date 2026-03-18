using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class FindYourCenterEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "find-your-center";
    public override string TemplateId => "named.find-your-center";

    public override void OnSpellOrGearPlay(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        string actionId
    )
    {
        runtime.DrawCards(player, 1);
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
            "Resolve",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["draw"] = "1",
                ["channeledRunes"] = channeled.ToString(),
            }
        );
    }
}
