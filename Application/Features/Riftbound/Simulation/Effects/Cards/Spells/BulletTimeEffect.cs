using System.Globalization;
using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class BulletTimeEffect : RiftboundNamedCardEffectBase
{
    private const string BattlefieldMarker = "-bullet-time-bf-";
    private const string AmountMarker = "-amount-";

    public override string NameIdentifier => "bullet-time";
    public override string TemplateId => "named.bullet-time";

    public override bool TryAddLegalActions(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        List<RiftboundLegalAction> actions
    )
    {
        var maxSpend = player.RunePool.PowerByDomain.Values.Sum()
            + player.BaseZone.Cards.Count(x =>
                string.Equals(x.Type, "Rune", StringComparison.OrdinalIgnoreCase) && !x.IsExhausted
            );
        var maxAmount = Math.Max(0, maxSpend);
        var battlefields = session.Battlefields.Where(x =>
            x.Units.Any(unit => unit.ControllerPlayerIndex != player.PlayerIndex)
        );
        foreach (var battlefield in battlefields)
        {
            for (var amount = 0; amount <= maxAmount; amount += 1)
            {
                actions.Add(
                    new RiftboundLegalAction(
                        $"{runtime.ActionPrefix}play-{card.InstanceId}-spell{BattlefieldMarker}{battlefield.Index}{AmountMarker}{amount}",
                        RiftboundActionType.PlayCard,
                        player.PlayerIndex,
                        $"Play {card.Name} at {battlefield.Name} for {amount} rune"
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
        var battlefield = ResolveBattlefield(session, actionId);
        if (battlefield is null)
        {
            return;
        }

        var amount = ResolveAmount(actionId);
        if (amount > 0)
        {
            var requirements = Enumerable
                .Range(0, amount)
                .Select(_ => new EffectPowerRequirement(1, null))
                .ToList();
            if (!runtime.TryPayCost(session, player, energyCost: 0, requirements))
            {
                return;
            }
        }

        var damage = amount + runtime.GetSpellAndAbilityBonusDamage(session, player.PlayerIndex);
        foreach (var enemy in battlefield.Units.Where(x => x.ControllerPlayerIndex != player.PlayerIndex))
        {
            enemy.MarkedDamage += damage;
        }

        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "Resolve",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["battlefield"] = battlefield.Name,
                ["paidRune"] = amount.ToString(CultureInfo.InvariantCulture),
                ["damage"] = damage.ToString(CultureInfo.InvariantCulture),
            }
        );
    }

    private static BattlefieldState? ResolveBattlefield(GameSession session, string actionId)
    {
        var markerIndex = actionId.IndexOf(BattlefieldMarker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return null;
        }

        var fragment = actionId[(markerIndex + BattlefieldMarker.Length)..];
        var amountIndex = fragment.IndexOf(AmountMarker, StringComparison.Ordinal);
        var battlefieldText = amountIndex >= 0 ? fragment[..amountIndex] : fragment;
        if (!int.TryParse(battlefieldText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index))
        {
            return null;
        }

        return session.Battlefields.FirstOrDefault(x => x.Index == index);
    }

    private static int ResolveAmount(string actionId)
    {
        var markerIndex = actionId.IndexOf(AmountMarker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return 0;
        }

        var fragment = actionId[(markerIndex + AmountMarker.Length)..];
        if (fragment.EndsWith("-repeat", StringComparison.Ordinal))
        {
            fragment = fragment[..^"-repeat".Length];
        }

        return int.TryParse(fragment, NumberStyles.Integer, CultureInfo.InvariantCulture, out var amount)
            ? Math.Max(0, amount)
            : 0;
    }
}
