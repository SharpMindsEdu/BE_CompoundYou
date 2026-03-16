using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class MeditationEffect : RiftboundNamedCardEffectBase
{
    private const string ExhaustUnitMarker = "-exhaust-unit-";

    public override string NameIdentifier => "meditation";
    public override string TemplateId => "named.meditation";

    protected override IReadOnlyDictionary<string, string> BuildData(
        RiftboundCard card,
        string normalizedEffectText
    )
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["drawWithoutExhaust"] = "1",
            ["drawWithExhaust"] = "2",
        };
    }

    public override bool TryAddLegalActions(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        List<RiftboundLegalAction> actions
    )
    {
        var baseAction = $"{runtime.ActionPrefix}play-{card.InstanceId}-spell";
        actions.Add(
            new RiftboundLegalAction(
                baseAction,
                RiftboundActionType.PlayCard,
                player.PlayerIndex,
                $"Play spell {card.Name}"
            )
        );

        foreach (
            var unit in RiftboundEffectUnitTargeting.EnumerateFriendlyUnits(session, player.PlayerIndex)
        )
        {
            if (unit.IsExhausted)
            {
                continue;
            }

            actions.Add(
                new RiftboundLegalAction(
                    $"{baseAction}{ExhaustUnitMarker}{unit.InstanceId}",
                    RiftboundActionType.PlayCard,
                    player.PlayerIndex,
                    $"Play {card.Name} and exhaust {unit.Name}"
                )
            );
        }

        return true;
    }

    public override void OnSpellOrGearPlay(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        string actionId
    )
    {
        var drawWithoutExhaust = Math.Max(
            0,
            runtime.ReadIntEffectData(card, "drawWithoutExhaust", fallback: 1)
        );
        var drawWithExhaust = Math.Max(
            drawWithoutExhaust,
            runtime.ReadIntEffectData(card, "drawWithExhaust", fallback: 2)
        );

        var exhaustedUnit = ResolveExhaustedUnit(session, actionId);
        var usedAdditionalCost = exhaustedUnit is not null
            && exhaustedUnit.ControllerPlayerIndex == player.PlayerIndex
            && !exhaustedUnit.IsExhausted;
        if (usedAdditionalCost)
        {
            exhaustedUnit!.IsExhausted = true;
        }

        var drawCount = usedAdditionalCost ? drawWithExhaust : drawWithoutExhaust;
        runtime.DrawCards(player, drawCount);

        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["template"] = card.EffectTemplateId,
            ["draw"] = drawCount.ToString(),
            ["usedAdditionalCost"] = usedAdditionalCost ? "true" : "false",
        };
        if (usedAdditionalCost)
        {
            metadata["exhaustedUnit"] = exhaustedUnit!.Name;
        }

        runtime.AddEffectContext(session, card.Name, player.PlayerIndex, "Resolve", metadata);
    }

    private static CardInstance? ResolveExhaustedUnit(GameSession session, string actionId)
    {
        var markerIndex = actionId.IndexOf(ExhaustUnitMarker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return null;
        }

        var fragment = actionId[(markerIndex + ExhaustUnitMarker.Length)..];
        if (!Guid.TryParse(fragment, out var unitId))
        {
            return null;
        }

        return RiftboundEffectUnitTargeting
            .EnumerateAllUnits(session)
            .FirstOrDefault(x => x.InstanceId == unitId);
    }
}
