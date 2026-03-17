using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class ObeliskOfPowerEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "obelisk-of-power";
    public override string TemplateId => "named.obelisk-of-power";

    public override void OnBattlefieldBeginning(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        BattlefieldState battlefield
    )
    {
        var battlefieldIndex = battlefield.Index.ToString();
        var alreadyTriggered = session.EffectContexts.Any(context =>
            context.ControllerPlayerIndex == player.PlayerIndex
            && string.Equals(context.Source, card.Name, StringComparison.OrdinalIgnoreCase)
            && string.Equals(context.Timing, "Beginning", StringComparison.OrdinalIgnoreCase)
            && context.Metadata.TryGetValue("battlefieldIndex", out var indexText)
            && string.Equals(indexText, battlefieldIndex, StringComparison.Ordinal)
        );
        if (alreadyTriggered)
        {
            return;
        }

        var channeledRunes = 0;
        if (player.RuneDeckZone.Cards.Count > 0)
        {
            var rune = player.RuneDeckZone.Cards[0];
            player.RuneDeckZone.Cards.RemoveAt(0);
            player.BaseZone.Cards.Add(rune);
            channeledRunes = 1;
        }

        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "Beginning",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["battlefield"] = battlefield.Name,
                ["battlefieldIndex"] = battlefieldIndex,
                ["channeledRunes"] = channeledRunes.ToString(),
            }
        );
    }
}

