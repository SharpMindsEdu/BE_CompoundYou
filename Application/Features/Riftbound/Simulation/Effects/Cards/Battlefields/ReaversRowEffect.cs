using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class ReaversRowEffect : RiftboundNamedCardEffectBase
{
    public const string PendingChoiceKind = "reavers-row-when-defend";

    public override string NameIdentifier => "reaver-s-row";
    public override string TemplateId => "named.reaver-s-row";

    public override void OnBattlefieldShowdownStart(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        CardInstance card,
        BattlefieldState battlefield,
        int attackerPlayerIndex,
        int defenderPlayerIndex,
        string? sourceActionId
    )
    {
        var defender = session.Players.FirstOrDefault(x => x.PlayerIndex == defenderPlayerIndex);
        if (defender is null)
        {
            return;
        }

        var candidateUnits = battlefield.Units
            .Where(x => x.ControllerPlayerIndex == defenderPlayerIndex)
            .OrderBy(x => runtime.GetEffectiveMight(session, x))
            .ThenBy(x => x.Name, StringComparer.Ordinal)
            .ThenBy(x => x.InstanceId)
            .ToList();
        if (candidateUnits.Count == 0)
        {
            runtime.AddEffectContext(
                session,
                card.Name,
                defenderPlayerIndex,
                "WhenDefend",
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["template"] = card.EffectTemplateId,
                    ["battlefield"] = battlefield.Name,
                    ["movedUnit"] = string.Empty,
                    ["moved"] = "false",
                }
            );
            return;
        }

        if (session.PendingChoice is not null)
        {
            return;
        }

        var options = new List<PendingChoiceOption>
        {
            new()
            {
                ActionId = $"{runtime.ActionPrefix}choose-reavers-row-none",
                Description = $"Reaver's Row: keep all units at {battlefield.Name}",
                Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["choice"] = "none",
                },
            },
        };
        options.AddRange(
            candidateUnits.Select(unit => new PendingChoiceOption
            {
                ActionId = $"{runtime.ActionPrefix}choose-reavers-row-move-{unit.InstanceId}",
                Description = $"Reaver's Row: move {unit.Name} to base",
                Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["choice"] = "move",
                    ["unitId"] = unit.InstanceId.ToString(),
                },
            })
        );

        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["battlefieldIndex"] = battlefield.Index.ToString(),
            ["defenderPlayerIndex"] = defenderPlayerIndex.ToString(),
            ["attackerPlayerIndex"] = attackerPlayerIndex.ToString(),
        };
        if (!string.IsNullOrWhiteSpace(sourceActionId))
        {
            metadata["sourceActionId"] = sourceActionId;
        }

        session.PendingChoice = new PendingChoiceState
        {
            Kind = PendingChoiceKind,
            PlayerIndex = defenderPlayerIndex,
            SourceCardInstanceId = card.InstanceId,
            SourceCardName = card.Name,
            Options = options,
            Metadata = metadata,
        };
    }

    internal static void ResolvePendingChoice(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PendingChoiceState pendingChoice,
        PendingChoiceOption option
    )
    {
        if (
            !pendingChoice.Metadata.TryGetValue("battlefieldIndex", out var battlefieldIndexText)
            || !int.TryParse(battlefieldIndexText, out var battlefieldIndex)
        )
        {
            return;
        }

        var battlefield = session.Battlefields.FirstOrDefault(x => x.Index == battlefieldIndex);
        if (battlefield is null)
        {
            return;
        }

        var defender = session.Players.FirstOrDefault(x => x.PlayerIndex == pendingChoice.PlayerIndex);
        if (defender is null)
        {
            return;
        }

        CardInstance? movedUnit = null;
        if (
            option.Metadata.TryGetValue("choice", out var choice)
            && string.Equals(choice, "move", StringComparison.OrdinalIgnoreCase)
            && option.Metadata.TryGetValue("unitId", out var unitIdText)
            && Guid.TryParse(unitIdText, out var unitId)
        )
        {
            movedUnit = battlefield.Units.FirstOrDefault(x =>
                x.InstanceId == unitId && x.ControllerPlayerIndex == defender.PlayerIndex
            );
            if (movedUnit is not null)
            {
                battlefield.Units.Remove(movedUnit);
                defender.BaseZone.Cards.Add(movedUnit);
            }
        }

        runtime.AddEffectContext(
            session,
            pendingChoice.SourceCardName,
            defender.PlayerIndex,
            "WhenDefend",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = TemplateIdStatic,
                ["battlefield"] = battlefield.Name,
                ["movedUnit"] = movedUnit?.Name ?? string.Empty,
                ["moved"] = (movedUnit is not null).ToString().ToLowerInvariant(),
            }
        );
    }

    private const string TemplateIdStatic = "named.reaver-s-row";
}
