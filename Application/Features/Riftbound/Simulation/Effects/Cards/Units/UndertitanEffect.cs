using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class UndertitanEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "undertitan";
    public override string TemplateId => "named.undertitan";

    protected override IReadOnlyDictionary<string, string> BuildData(
        RiftboundCard card,
        string normalizedEffectText
    )
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["onPlayBuffOtherFriendlyUnits"] = "2",
            ["topDeckReveal.addEnergy"] = "2",
        };
    }

    public override void OnUnitPlay(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        string actionId
    )
    {
        var magnitude = runtime.ReadIntEffectData(card, "onPlayBuffOtherFriendlyUnits", fallback: 2);
        if (magnitude <= 0)
        {
            return;
        }

        var buffed = 0;
        foreach (
            var friendlyUnit in RiftboundEffectUnitTargeting.EnumerateFriendlyUnits(
                session,
                player.PlayerIndex
            )
        )
        {
            if (friendlyUnit.InstanceId == card.InstanceId)
            {
                continue;
            }

            friendlyUnit.TemporaryMightModifier += magnitude;
            buffed += 1;
        }

        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "WhenPlay",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["buffedUnits"] = buffed.ToString(),
                ["magnitude"] = magnitude.ToString(),
            }
        );
    }
}
