using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class KingsEdictEffect : RiftboundNamedCardEffectBase
{
    public const string PendingChoiceKind = "kings-edict-choice";

    private const string CasterPlayerIndexKey = "casterPlayerIndex";
    private const string RemainingChooserIndicesKey = "remainingChooserIndices";
    private const string ChosenUnitIdsKey = "chosenUnitIds";
    private const string ChosenTargetIdKey = "chosenTargetId";
    private const string SourceTemplateKey = "template";
    private const string SourceTemplateFallback = "named.kings-edict";

    public override string NameIdentifier => "king-s-edict";
    public override string TemplateId => SourceTemplateFallback;

    public override void OnSpellOrGearPlay(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        string actionId
    )
    {
        var chooserOrder = BuildChooserOrder(session, player.PlayerIndex);
        BeginOrAdvanceChoice(
            runtime,
            session,
            card.Name,
            card.EffectTemplateId,
            card.InstanceId,
            player.PlayerIndex,
            chooserOrder,
            []
        );
    }

    internal static void ResolvePendingChoice(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PendingChoiceState pendingChoice,
        PendingChoiceOption option
    )
    {
        if (
            !pendingChoice.Metadata.TryGetValue(CasterPlayerIndexKey, out var casterPlayerIndexText)
            || !int.TryParse(casterPlayerIndexText, out var casterPlayerIndex)
        )
        {
            return;
        }

        var target = ResolveTargetFromOption(session, option);
        if (target is not null && target.ControllerPlayerIndex != casterPlayerIndex)
        {
            var damage = runtime.GetEffectiveMight(session, target);
            target.MarkedDamage += damage;
        }

        var chosenUnitIds = ParseGuidList(
            pendingChoice.Metadata.TryGetValue(ChosenUnitIdsKey, out var chosenText)
                ? chosenText
                : string.Empty
        );
        if (target is not null)
        {
            chosenUnitIds.Add(target.InstanceId);
        }

        runtime.AddEffectContext(
            session,
            pendingChoice.SourceCardName,
            casterPlayerIndex,
            "Resolve",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [SourceTemplateKey] = pendingChoice.Metadata.TryGetValue(SourceTemplateKey, out var template)
                    ? template
                    : SourceTemplateFallback,
                ["chooserPlayerIndex"] = pendingChoice.PlayerIndex.ToString(),
                ["chosenTarget"] = target?.Name ?? string.Empty,
            }
        );

        var remainingChoosers = ParseIntList(
            pendingChoice.Metadata.TryGetValue(RemainingChooserIndicesKey, out var remainingText)
                ? remainingText
                : string.Empty
        );
        BeginOrAdvanceChoice(
            runtime,
            session,
            pendingChoice.SourceCardName,
            pendingChoice.Metadata.TryGetValue(SourceTemplateKey, out var resolvedTemplate)
                ? resolvedTemplate
                : SourceTemplateFallback,
            pendingChoice.SourceCardInstanceId,
            casterPlayerIndex,
            remainingChoosers,
            chosenUnitIds
        );
    }

    private static void BeginOrAdvanceChoice(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        string sourceCardName,
        string sourceTemplateId,
        Guid sourceCardInstanceId,
        int casterPlayerIndex,
        IReadOnlyCollection<int> chooserOrder,
        IReadOnlyCollection<Guid> chosenUnitIds
    )
    {
        if (session.PendingChoice is not null)
        {
            return;
        }

        var remaining = chooserOrder.ToList();
        while (remaining.Count > 0)
        {
            var chooserIndex = remaining[0];
            remaining.RemoveAt(0);
            var candidates = RiftboundEffectUnitTargeting
                .EnumerateAllUnits(session)
                .Where(x =>
                    x.ControllerPlayerIndex != casterPlayerIndex
                    && !chosenUnitIds.Contains(x.InstanceId)
                )
                .OrderBy(x => x.Name, StringComparer.Ordinal)
                .ThenBy(x => x.InstanceId)
                .ToList();
            if (candidates.Count == 0)
            {
                continue;
            }

            session.PendingChoice = new PendingChoiceState
            {
                Kind = PendingChoiceKind,
                PlayerIndex = chooserIndex,
                SourceCardInstanceId = sourceCardInstanceId,
                SourceCardName = sourceCardName,
                Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [SourceTemplateKey] = sourceTemplateId,
                    [CasterPlayerIndexKey] = casterPlayerIndex.ToString(),
                    [RemainingChooserIndicesKey] = string.Join(",", remaining),
                    [ChosenUnitIdsKey] = string.Join(
                        ",",
                        chosenUnitIds.Select(x => x.ToString())
                    ),
                },
                Options = candidates
                    .Select(candidate => new PendingChoiceOption
                    {
                        ActionId = $"{runtime.ActionPrefix}choose-kings-edict-{candidate.InstanceId}",
                        Description = $"King's Edict: choose {candidate.Name}",
                        Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                        {
                            [ChosenTargetIdKey] = candidate.InstanceId.ToString(),
                        },
                    })
                    .ToList(),
            };
            return;
        }
    }

    private static List<int> BuildChooserOrder(GameSession session, int casterPlayerIndex)
    {
        var playerIndices = session.Players.Select(x => x.PlayerIndex).ToList();
        var casterPosition = playerIndices.IndexOf(casterPlayerIndex);
        if (casterPosition < 0)
        {
            return [];
        }

        var order = new List<int>();
        for (var offset = 1; offset < playerIndices.Count; offset += 1)
        {
            var chooserPosition = (casterPosition + offset) % playerIndices.Count;
            order.Add(playerIndices[chooserPosition]);
        }

        return order;
    }

    private static CardInstance? ResolveTargetFromOption(
        GameSession session,
        PendingChoiceOption option
    )
    {
        if (
            !option.Metadata.TryGetValue(ChosenTargetIdKey, out var chosenTargetIdText)
            || !Guid.TryParse(chosenTargetIdText, out var chosenTargetId)
        )
        {
            return null;
        }

        return RiftboundEffectUnitTargeting
            .EnumerateAllUnits(session)
            .FirstOrDefault(x => x.InstanceId == chosenTargetId);
    }

    private static HashSet<Guid> ParseGuidList(string raw)
    {
        var parsed = new HashSet<Guid>();
        foreach (
            var token in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        )
        {
            if (Guid.TryParse(token, out var id))
            {
                parsed.Add(id);
            }
        }

        return parsed;
    }

    private static List<int> ParseIntList(string raw)
    {
        var parsed = new List<int>();
        foreach (
            var token in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        )
        {
            if (int.TryParse(token, out var id))
            {
                parsed.Add(id);
            }
        }

        return parsed;
    }
}
