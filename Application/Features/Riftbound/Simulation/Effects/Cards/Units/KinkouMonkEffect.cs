using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class KinkouMonkEffect : RiftboundNamedCardEffectBase
{
    public const string PendingChoiceKind = "kinkou-monk-buff-choice";
    private const string ChosenUnitIdsKey = "chosenUnitIds";
    private const string RemainingChoicesKey = "remainingChoices";
    private const string ChooseUnitIdKey = "chosenUnitId";
    private const string SourceTemplateFallback = "named.kinkou-monk";

    public override string NameIdentifier => "kinkou-monk";
    public override string TemplateId => SourceTemplateFallback;

    public override void OnUnitPlay(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        string actionId
    )
    {
        var candidates = ResolveCandidates(session, player.PlayerIndex, card.InstanceId);
        if (candidates.Count == 0 || session.PendingChoice is not null)
        {
            return;
        }

        session.PendingChoice = BuildPendingChoice(runtime, player, card, candidates, [], 2);
    }

    internal static void ResolvePendingChoice(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PendingChoiceState pendingChoice,
        PendingChoiceOption option
    )
    {
        var player = session.Players.FirstOrDefault(x => x.PlayerIndex == pendingChoice.PlayerIndex);
        if (player is null)
        {
            return;
        }

        var sourceCard = RiftboundEffectCardLookup.FindCardByInstanceId(
            session,
            pendingChoice.SourceCardInstanceId
        );
        if (sourceCard is null)
        {
            return;
        }

        var selectedIds = ParseGuidList(
            pendingChoice.Metadata.TryGetValue(ChosenUnitIdsKey, out var selectedText)
                ? selectedText
                : string.Empty
        );
        var remainingChoices = pendingChoice.Metadata.TryGetValue(RemainingChoicesKey, out var remainingText)
            && int.TryParse(remainingText, out var parsedRemaining)
            ? Math.Max(0, parsedRemaining)
            : 0;

        if (
            option.Metadata.TryGetValue(ChooseUnitIdKey, out var chosenUnitIdText)
            && Guid.TryParse(chosenUnitIdText, out var chosenUnitId)
        )
        {
            selectedIds.Add(chosenUnitId);
            remainingChoices = Math.Max(0, remainingChoices - 1);
        }
        else
        {
            remainingChoices = 0;
        }

        var candidates = ResolveCandidates(session, player.PlayerIndex, sourceCard.InstanceId)
            .Where(x => !selectedIds.Contains(x.InstanceId))
            .ToList();
        if (remainingChoices > 0 && candidates.Count > 0)
        {
            session.PendingChoice = BuildPendingChoice(
                runtime,
                player,
                sourceCard,
                candidates,
                selectedIds,
                remainingChoices
            );
            return;
        }

        var allFriendlyUnits = RiftboundEffectUnitTargeting.EnumerateFriendlyUnits(
            session,
            player.PlayerIndex
        );
        var selectedUnits = allFriendlyUnits
            .Where(x => selectedIds.Contains(x.InstanceId))
            .DistinctBy(x => x.InstanceId)
            .ToList();
        if (selectedUnits.Count == 0)
        {
            return;
        }

        var buffed = 0;
        foreach (var unit in selectedUnits)
        {
            if (unit.PermanentMightModifier > 0)
            {
                continue;
            }

            unit.PermanentMightModifier += 1;
            buffed += 1;
        }

        runtime.AddEffectContext(
            session,
            sourceCard.Name,
            player.PlayerIndex,
            "WhenPlay",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = sourceCard.EffectTemplateId,
                ["selectedUnits"] = selectedUnits.Count.ToString(),
                ["buffedUnits"] = buffed.ToString(),
            }
        );
    }

    private static PendingChoiceState BuildPendingChoice(
        IRiftboundEffectRuntime runtime,
        PlayerState player,
        CardInstance sourceCard,
        IReadOnlyCollection<CardInstance> candidates,
        IReadOnlyCollection<Guid> selectedIds,
        int remainingChoices
    )
    {
        var options = candidates
            .Select(candidate => new PendingChoiceOption
            {
                ActionId = $"{runtime.ActionPrefix}choose-kinkou-monk-{candidate.InstanceId}",
                Description = $"Kinkou Monk: choose {candidate.Name}",
                Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [ChooseUnitIdKey] = candidate.InstanceId.ToString(),
                },
            })
            .ToList();
        options.Add(
            new PendingChoiceOption
            {
                ActionId = $"{runtime.ActionPrefix}choose-kinkou-monk-done",
                Description = "Kinkou Monk: done",
                Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            }
        );

        return new PendingChoiceState
        {
            Kind = PendingChoiceKind,
            PlayerIndex = player.PlayerIndex,
            SourceCardInstanceId = sourceCard.InstanceId,
            SourceCardName = sourceCard.Name,
            Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = sourceCard.EffectTemplateId,
                [ChosenUnitIdsKey] = string.Join(",", selectedIds.Select(x => x.ToString())),
                [RemainingChoicesKey] = remainingChoices.ToString(),
            },
            Options = options,
        };
    }

    private static List<CardInstance> ResolveCandidates(
        GameSession session,
        int playerIndex,
        Guid sourceUnitId
    )
    {
        return RiftboundEffectUnitTargeting
            .EnumerateFriendlyUnits(session, playerIndex)
            .Where(x => x.InstanceId != sourceUnitId)
            .OrderByDescending(x => x.Might.GetValueOrDefault() + x.PermanentMightModifier)
            .ThenBy(x => x.Name, StringComparer.Ordinal)
            .ThenBy(x => x.InstanceId)
            .ToList();
    }

    private static HashSet<Guid> ParseGuidList(string raw)
    {
        var result = new HashSet<Guid>();
        foreach (
            var item in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        )
        {
            if (Guid.TryParse(item, out var parsed))
            {
                result.Add(parsed);
            }
        }

        return result;
    }
}
