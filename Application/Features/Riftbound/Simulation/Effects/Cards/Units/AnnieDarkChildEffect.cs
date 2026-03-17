using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class AnnieDarkChildEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "annie-dark-child";
    public override string TemplateId => "named.annie-dark-child";

    public override void OnEndTurn(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card
    )
    {
        var readied = 0;
        foreach (
            var rune in player.BaseZone.Cards
                .Where(x =>
                    string.Equals(x.Type, "Rune", StringComparison.OrdinalIgnoreCase) && x.IsExhausted
                )
                .Take(2)
        )
        {
            rune.IsExhausted = false;
            readied += 1;
        }

        if (readied <= 0)
        {
            return;
        }

        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "EndTurn",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["readyRunes"] = readied.ToString(),
            }
        );
    }
}
