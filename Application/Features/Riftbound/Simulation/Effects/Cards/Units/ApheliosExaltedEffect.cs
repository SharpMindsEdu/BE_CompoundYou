using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class ApheliosExaltedEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "aphelios-exalted";
    public override string TemplateId => "named.aphelios-exalted";

    public override void OnGearAttached(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        CardInstance attachedGear,
        CardInstance targetUnit
    )
    {
        if (targetUnit.InstanceId != card.InstanceId)
        {
            return;
        }

        var availableChoices = ResolveAvailableChoices(session, card);
        if (availableChoices.Count == 0)
        {
            return;
        }

        var selectedChoice = availableChoices[0];
        if (string.Equals(selectedChoice, "ready-runes", StringComparison.Ordinal))
        {
            foreach (
                var rune in player.BaseZone.Cards
                    .Where(x =>
                        string.Equals(x.Type, "Rune", StringComparison.OrdinalIgnoreCase)
                        && x.IsExhausted
                    )
                    .Take(2)
            )
            {
                rune.IsExhausted = false;
            }
        }
        else if (string.Equals(selectedChoice, "channel-exhausted", StringComparison.Ordinal))
        {
            if (player.RuneDeckZone.Cards.Count > 0)
            {
                var rune = player.RuneDeckZone.Cards[0];
                player.RuneDeckZone.Cards.RemoveAt(0);
                rune.IsExhausted = true;
                player.BaseZone.Cards.Add(rune);
            }
        }
        else if (string.Equals(selectedChoice, "buff-friendly", StringComparison.Ordinal))
        {
            var target = RiftboundEffectUnitTargeting.EnumerateFriendlyUnits(session, player.PlayerIndex)
                .OrderByDescending(x => x.Might.GetValueOrDefault() + x.PermanentMightModifier + x.TemporaryMightModifier)
                .ThenBy(x => x.Name, StringComparer.Ordinal)
                .ThenBy(x => x.InstanceId)
                .FirstOrDefault();
            if (target is not null)
            {
                target.PermanentMightModifier += 1;
            }
        }

        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "WhenEquipAttached",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["choice"] = selectedChoice,
                ["instanceId"] = card.InstanceId.ToString(),
                ["gear"] = attachedGear.Name,
            }
        );
    }

    private static List<string> ResolveAvailableChoices(GameSession session, CardInstance aphelios)
    {
        var used = session.EffectContexts
            .Where(x =>
                string.Equals(x.Source, aphelios.Name, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.Timing, "WhenEquipAttached", StringComparison.OrdinalIgnoreCase)
                && x.Metadata.TryGetValue("instanceId", out var id)
                && string.Equals(id, aphelios.InstanceId.ToString(), StringComparison.OrdinalIgnoreCase)
                && x.Metadata.TryGetValue("turn", out var turnText)
                && int.TryParse(turnText, out var turn)
                && turn == session.TurnNumber
            )
            .Select(x => x.Metadata.TryGetValue("choice", out var choice) ? choice : string.Empty)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var orderedChoices = new List<string> { "ready-runes", "channel-exhausted", "buff-friendly" };
        return orderedChoices.Where(x => !used.Contains(x)).ToList();
    }
}
