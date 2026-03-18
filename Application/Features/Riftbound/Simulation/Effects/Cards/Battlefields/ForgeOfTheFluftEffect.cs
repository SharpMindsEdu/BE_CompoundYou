using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class ForgeOfTheFluftEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "forge-of-the-fluft";
    public override string TemplateId => "named.forge-of-the-fluft";

    protected override IReadOnlyDictionary<string, string> BuildData(
        RiftboundCard card,
        string normalizedEffectText
    )
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["grantsLegendEquipAbility"] = "true",
        };
    }

    public override void OnBattlefieldBeginning(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        BattlefieldState battlefield
    )
    {
        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "Aura",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["battlefieldIndex"] = battlefield.Index.ToString(),
                ["grantsLegendEquipAbility"] = "true",
            }
        );
    }
}
