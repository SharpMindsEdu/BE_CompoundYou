using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class KatoTheArmEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "kato-the-arm";
    public override string TemplateId => "named.kato-the-arm";

    public override void OnUnitMove(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card
    )
    {
        var battlefield = RiftboundEffectUnitTargeting.FindBattlefieldContainingUnit(
            session,
            card.InstanceId
        );
        if (battlefield is null)
        {
            return;
        }

        var target = battlefield.Units
            .Where(x =>
                x.ControllerPlayerIndex == player.PlayerIndex
                && x.InstanceId != card.InstanceId
            )
            .OrderByDescending(x => runtime.GetEffectiveMight(session, x))
            .ThenBy(x => x.Name, StringComparer.Ordinal)
            .ThenBy(x => x.InstanceId)
            .FirstOrDefault();
        if (target is null)
        {
            return;
        }

        var grantedMight = runtime.GetEffectiveMight(session, card);
        target.TemporaryMightModifier += grantedMight;

        var grantedKeywords = card.Keywords
            .Where(x =>
                !string.IsNullOrWhiteSpace(x)
                && !string.Equals(x, "Attacker", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(x, "Defender", StringComparison.OrdinalIgnoreCase)
            )
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        foreach (var keyword in grantedKeywords)
        {
            target.EffectData[$"temporaryKeyword.{keyword}"] = "true";
        }

        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "WhenMove",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["target"] = target.Name,
                ["temporaryMight"] = grantedMight.ToString(),
                ["grantedKeywords"] = string.Join(",", grantedKeywords),
            }
        );
    }
}
