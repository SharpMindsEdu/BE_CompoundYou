using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class StackedDeckEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "stacked-deck";
    public override string TemplateId => "named.stacked-deck";

    protected override IReadOnlyDictionary<string, string> BuildData(
        RiftboundCard card,
        string normalizedEffectText
    )
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["magnitude"] = "1",
        };
    }

    public override void OnSpellOrGearPlay(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        string actionId
    )
    {
        runtime.DrawCards(player, runtime.ReadMagnitude(card, fallback: 1));
    }
}
