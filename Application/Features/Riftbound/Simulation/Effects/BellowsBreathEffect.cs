using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class BellowsBreathEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "bellows-breath";
    public override string TemplateId => "named.bellows-breath";

    protected override IReadOnlyDictionary<string, string> BuildData(
        RiftboundCard card,
        string normalizedEffectText
    )
    {
        var data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["magnitude"] = (RiftboundEffectTextParser.TryExtractMagnitude(normalizedEffectText) ?? 1).ToString(),
            ["maxTargets"] = RiftboundEffectTextParser.TryExtractUnitCount(normalizedEffectText, fallback: 3).ToString(),
        };

        var repeatSection = RiftboundEffectTextParser.TryExtractRepeatSection(normalizedEffectText);
        if (string.IsNullOrWhiteSpace(repeatSection))
        {
            return data;
        }

        var repeatEnergy = RiftboundEffectTextParser.TryExtractEnergyIconValue(repeatSection);
        if (repeatEnergy.HasValue && repeatEnergy.Value > 0)
        {
            data["repeatEnergyCost"] = repeatEnergy.Value.ToString();
        }

        var repeatDomain = RiftboundEffectTextParser.TryExtractRuneDomain(repeatSection);
        if (!string.IsNullOrWhiteSpace(repeatDomain))
        {
            data[$"repeatPowerCost.{repeatDomain}"] = "1";
        }

        return data;
    }

    public override bool TryAddLegalActions(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        List<RiftboundLegalAction> actions
    )
    {
        var maxTargets = runtime.ReadIntEffectData(card, "maxTargets", fallback: 3);
        var canChooseRepeat =
            runtime.ReadIntEffectData(card, "repeatEnergyCost", fallback: 0) > 0
            || card.EffectData.Keys.Any(key =>
                key.StartsWith("repeatPowerCost.", StringComparison.OrdinalIgnoreCase)
            );
        var targetSelections = runtime.EnumerateSameLocationEnemyTargetSelections(
            session,
            player.PlayerIndex,
            maxTargets
        );

        foreach (var selection in targetSelections)
        {
            var targetList = string.Join(",", selection.Targets.Select(x => x.InstanceId.ToString()));
            var actionId =
                $"{runtime.ActionPrefix}play-{card.InstanceId}-spell{runtime.MultiTargetUnitsMarker}{targetList}";
            actions.Add(
                new RiftboundLegalAction(
                    actionId,
                    RiftboundActionType.PlayCard,
                    player.PlayerIndex,
                    $"Play {card.Name} targeting {selection.Targets.Count} unit(s) at {selection.LocationKey}"
                )
            );

            if (canChooseRepeat)
            {
                actions.Add(
                    new RiftboundLegalAction(
                        $"{actionId}{runtime.RepeatActionSuffix}",
                        RiftboundActionType.PlayCard,
                        player.PlayerIndex,
                        $"Play {card.Name} targeting {selection.Targets.Count} unit(s) at {selection.LocationKey} (repeat)"
                    )
                );
            }
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
        var magnitude = runtime.ReadMagnitude(card, fallback: 1);
        var maxTargets = runtime.ReadIntEffectData(card, "maxTargets", fallback: 3);
        var firstSelection = runtime.ResolveSelectedSameLocationEnemyTargets(
            session,
            player.PlayerIndex,
            actionId,
            maxTargets
        );
        if (firstSelection.Targets.Count == 0)
        {
            return;
        }

        foreach (var target in firstSelection.Targets)
        {
            target.MarkedDamage += magnitude;
        }

        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "Resolve",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["location"] = firstSelection.LocationKey,
                ["targets"] = firstSelection.Targets.Count.ToString(),
                ["repeat"] = "false",
            }
        );

        if (!runtime.IsRepeatRequested(actionId))
        {
            return;
        }

        if (!runtime.TryPayRepeatCost(session, player, card))
        {
            return;
        }

        foreach (var target in firstSelection.Targets)
        {
            target.MarkedDamage += magnitude;
        }

        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "Resolve",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["location"] = firstSelection.LocationKey,
                ["targets"] = firstSelection.Targets.Count.ToString(),
                ["repeat"] = "true",
            }
        );
    }
}
