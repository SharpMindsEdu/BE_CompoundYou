using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class AvaAchieverEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "ava-achiever";
    public override string TemplateId => "named.ava-achiever";

    public override void OnShowdownStart(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        BattlefieldState battlefield,
        bool isAttacker,
        bool isDefender
    )
    {
        if (!isAttacker)
        {
            return;
        }

        var hiddenCard = player.HandZone.Cards
            .Where(x => x.Keywords.Contains("Hidden", StringComparer.OrdinalIgnoreCase))
            .OrderByDescending(x => string.Equals(x.Type, "Unit", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(x => x.Cost.GetValueOrDefault())
            .ThenByDescending(x => x.Power.GetValueOrDefault())
            .ThenBy(x => x.Name, StringComparer.Ordinal)
            .ThenBy(x => x.InstanceId)
            .FirstOrDefault();
        if (hiddenCard is null)
        {
            return;
        }

        if (!runtime.TryPayCost(session, player, energyCost: 0, [new EffectPowerRequirement(1, ["Mind"])]))
        {
            return;
        }

        if (!player.HandZone.Cards.Remove(hiddenCard))
        {
            return;
        }

        player.MainDeckZone.Cards.Insert(0, hiddenCard);
        if (
            !runtime.TryPlayCardFromRevealIgnoringCost(
                session,
                player,
                hiddenCard,
                card,
                preferredBattlefieldIndex: battlefield.Index
            )
        )
        {
            player.MainDeckZone.Cards.Remove(hiddenCard);
            player.HandZone.Cards.Add(hiddenCard);
            return;
        }

        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "WhenAttack",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["playedHiddenCard"] = hiddenCard.Name,
                ["battlefield"] = battlefield.Name,
            }
        );
    }
}

