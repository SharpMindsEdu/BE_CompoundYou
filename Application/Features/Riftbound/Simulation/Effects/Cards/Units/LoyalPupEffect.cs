using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class LoyalPupEffect : RiftboundNamedCardEffectBase
{
    public const string PendingChoiceKind = "loyal-pup-defend-choice";

    public override string NameIdentifier => "loyal-pup";
    public override string TemplateId => "named.loyal-pup";

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
        if (session.PendingChoice is not null || card.ControllerPlayerIndex != defenderPlayerIndex)
        {
            return;
        }

        var defender = session.Players.FirstOrDefault(x => x.PlayerIndex == defenderPlayerIndex);
        if (
            defender is null
            || !defender.BaseZone.Cards.Any(x => x.InstanceId == card.InstanceId)
        )
        {
            return;
        }

        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["battlefieldIndex"] = battlefield.Index.ToString(),
            ["defenderPlayerIndex"] = defenderPlayerIndex.ToString(),
            ["unitId"] = card.InstanceId.ToString(),
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
            Metadata = metadata,
            Options =
            [
                new PendingChoiceOption
                {
                    ActionId = $"{runtime.ActionPrefix}choose-loyal-pup-stay",
                    Description = $"Loyal Pup: keep {card.Name} in base",
                    Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["choice"] = "stay",
                    },
                },
                new PendingChoiceOption
                {
                    ActionId = $"{runtime.ActionPrefix}choose-loyal-pup-move-{card.InstanceId}",
                    Description = $"Loyal Pup: move {card.Name} to {battlefield.Name}",
                    Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["choice"] = "move",
                    },
                },
            ],
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
            || !pendingChoice.Metadata.TryGetValue("unitId", out var unitIdText)
            || !Guid.TryParse(unitIdText, out var unitId)
        )
        {
            return;
        }

        var defender = session.Players.FirstOrDefault(x => x.PlayerIndex == pendingChoice.PlayerIndex);
        var battlefield = session.Battlefields.FirstOrDefault(x => x.Index == battlefieldIndex);
        if (defender is null || battlefield is null)
        {
            return;
        }

        var moved = false;
        CardInstance? movedUnit = null;
        if (
            option.Metadata.TryGetValue("choice", out var choice)
            && string.Equals(choice, "move", StringComparison.OrdinalIgnoreCase)
        )
        {
            movedUnit = defender.BaseZone.Cards.FirstOrDefault(x => x.InstanceId == unitId);
            if (movedUnit is not null)
            {
                defender.BaseZone.Cards.Remove(movedUnit);
                battlefield.Units.Add(movedUnit);
                if (battlefield.ControlledByPlayerIndex != defender.PlayerIndex)
                {
                    battlefield.ContestedByPlayerIndex = defender.PlayerIndex;
                }

                moved = true;
            }
        }

        runtime.AddEffectContext(
            session,
            pendingChoice.SourceCardName,
            pendingChoice.PlayerIndex,
            "WhenDefend",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = "named.loyal-pup",
                ["moved"] = moved.ToString().ToLowerInvariant(),
                ["battlefield"] = battlefield.Name,
                ["unit"] = movedUnit?.Name ?? pendingChoice.SourceCardName,
            }
        );
    }
}
