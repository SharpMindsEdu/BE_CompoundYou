using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class AzirSovereignEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "azir-sovereign";
    public override string TemplateId => "named.azir-sovereign";

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

        var myBattlefield = RiftboundEffectUnitTargeting.FindBattlefieldContainingUnit(session, card.InstanceId);
        if (myBattlefield is null)
        {
            return;
        }

        var moved = 0;
        var tokensFromBase = player.BaseZone.Cards
            .Where(x =>
                string.Equals(x.Type, "Unit", StringComparison.OrdinalIgnoreCase)
                && x.IsToken
                && x.InstanceId != card.InstanceId
            )
            .ToList();
        foreach (var token in tokensFromBase)
        {
            player.BaseZone.Cards.Remove(token);
            myBattlefield.Units.Add(token);
            moved += 1;
        }

        foreach (var sourceBattlefield in session.Battlefields.Where(x => x.Index != myBattlefield.Index))
        {
            var tokens = sourceBattlefield.Units
                .Where(x =>
                    x.ControllerPlayerIndex == player.PlayerIndex
                    && x.IsToken
                    && x.InstanceId != card.InstanceId
                )
                .ToList();
            foreach (var token in tokens)
            {
                sourceBattlefield.Units.Remove(token);
                myBattlefield.Units.Add(token);
                moved += 1;
            }
        }

        if (moved <= 0)
        {
            return;
        }

        if (myBattlefield.ControlledByPlayerIndex != player.PlayerIndex)
        {
            myBattlefield.ContestedByPlayerIndex = player.PlayerIndex;
        }

        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "WhenAttack",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["movedTokens"] = moved.ToString(),
                ["battlefield"] = myBattlefield.Name,
            }
        );
    }
}

