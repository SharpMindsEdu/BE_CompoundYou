using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class BlindFuryEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "blind-fury";
    public override string TemplateId => "named.blind-fury";

    public override void OnSpellOrGearPlay(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        string actionId
    )
    {
        var revealed = new List<(PlayerState Owner, CardInstance Card)>();
        foreach (var opponent in session.Players.Where(x => x.PlayerIndex != player.PlayerIndex))
        {
            if (opponent.MainDeckZone.Cards.Count == 0)
            {
                continue;
            }

            var topCard = opponent.MainDeckZone.Cards[0];
            opponent.MainDeckZone.Cards.RemoveAt(0);
            revealed.Add((opponent, topCard));
        }

        if (revealed.Count == 0)
        {
            return;
        }

        var chosen = revealed[0];
        var recycled = 0;
        foreach (var candidate in revealed.Skip(1))
        {
            candidate.Owner.MainDeckZone.Cards.Add(candidate.Card);
            recycled += 1;
        }

        var chosenCard = chosen.Card;
        chosenCard.IsFacedown = false;
        chosenCard.ControllerPlayerIndex = player.PlayerIndex;
        if (string.Equals(chosenCard.Type, "Unit", StringComparison.OrdinalIgnoreCase))
        {
            chosenCard.IsExhausted = true;
            player.BaseZone.Cards.Add(chosenCard);
        }
        else
        {
            player.TrashZone.Cards.Add(chosenCard);
        }

        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "Resolve",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["revealed"] = revealed.Count.ToString(),
                ["recycled"] = recycled.ToString(),
                ["playedCard"] = chosenCard.Name,
                ["playedIgnoringCost"] = "true",
            }
        );
    }
}

