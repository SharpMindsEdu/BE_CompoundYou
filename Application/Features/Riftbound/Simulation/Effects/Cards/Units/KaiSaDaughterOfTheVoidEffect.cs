using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class KaiSaDaughterOfTheVoidEffect : RiftboundNamedCardEffectBase
{
    private const string GenericRuneDomain = "__unknown__";

    public override string NameIdentifier => "kai-sa-daughter-of-the-void";
    public override string TemplateId => "named.kai-sa-daughter-of-the-void";
    public override bool HasActivatedAbility => true;

    public override bool TryActivateAbility(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card
    )
    {
        if (card.IsExhausted)
        {
            return false;
        }

        card.IsExhausted = true;
        runtime.AddPower(player, GenericRuneDomain, 1);
        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "Activate",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["addedRune"] = "1",
                ["spellOnly"] = "true",
            }
        );
        return true;
    }
}
