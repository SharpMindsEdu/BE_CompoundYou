using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class AnnieStubbornEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "annie-stubborn";
    public override string TemplateId => "named.annie-stubborn";

    public override void OnUnitPlay(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        string actionId
    )
    {
        var spell = player.TrashZone.Cards
            .Where(x => string.Equals(x.Type, "Spell", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.Cost.GetValueOrDefault())
            .ThenByDescending(x => x.Power.GetValueOrDefault())
            .ThenBy(x => x.Name, StringComparer.Ordinal)
            .ThenBy(x => x.InstanceId)
            .FirstOrDefault();
        if (spell is null)
        {
            return;
        }

        player.TrashZone.Cards.Remove(spell);
        player.HandZone.Cards.Add(spell);
        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "WhenPlay",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["returnedSpell"] = spell.Name,
            }
        );
    }
}
