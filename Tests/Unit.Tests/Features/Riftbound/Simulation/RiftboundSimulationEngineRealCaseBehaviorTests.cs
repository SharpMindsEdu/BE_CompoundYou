using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Unit.Tests.Features.Riftbound.Simulation;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.RiftboundTests)]
public sealed class RiftboundSimulationEngineRealCaseBehaviorTests : RiftboundSimulationEngineBehaviorTestBase
{
    [Fact]
    public void RealCase_FizzBellowsAndSealOfDiscord_ResolvesExpectedState()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                20260316,
                RiftboundSimulationTestData.BuildDeck(9100, "Chaos"),
                RiftboundSimulationTestData.BuildDeck(9200, "Order")
            )
        );

        var player = session.Players[0];
        var opponent = session.Players[1];
        player.HandZone.Cards.Clear();
        player.BaseZone.Cards.Clear();
        player.TrashZone.Cards.Clear();
        player.MainDeckZone.Cards.Clear();
        player.RunePool.Energy = 0;
        player.RunePool.PowerByDomain.Clear();

        var chaosRunes = Enumerable
            .Range(0, 6)
            .Select(i => BuildRuneInstance(100_000 + i, "Chaos Rune", "Chaos", ownerPlayer: 0))
            .ToList();
        var mindRunes = Enumerable
            .Range(0, 4)
            .Select(i => BuildRuneInstance(101_000 + i, "Mind Rune", "Mind", ownerPlayer: 0))
            .ToList();
        player.BaseZone.Cards.AddRange(chaosRunes);
        player.BaseZone.Cards.AddRange(mindRunes);

        var sealCard = new RiftboundCard
        {
            Id = 200_001,
            Cost = 0,
            Power = 1,
            Name = "Seal of Discord",
            Type = "Gear",
            Color = ["Chaos"],
            Effect = ":rb_exhaust:: [Reaction] — [Add] :rb_rune_chaos:. (Abilities that add resources can't be reacted to.)",
        };
        var sealA = BuildCardInstance(sealCard, ownerPlayer: 0, controllerPlayer: 0);
        var sealB = BuildCardInstance(sealCard, ownerPlayer: 0, controllerPlayer: 0);
        player.BaseZone.Cards.Add(sealA);
        player.BaseZone.Cards.Add(sealB);

        var fizzCard = new RiftboundCard
        {
            Id = 300_001,
            Name = "Fizz - Trickster",
            Type = "Unit",
            Supertype = "Champion",
            Cost = 3,
            Power = 1,
            Might = 2,
            Color = ["Chaos"],
            Effect = "When you play me, you may play a spell from your trash with Energy cost no more than :rb_energy_3:, ignoring its Energy cost. Recycle that spell after you play it. (You must still pay its Power cost.)",
        };
        var fizz = BuildCardInstance(fizzCard, ownerPlayer: 0, controllerPlayer: 0);
        player.HandZone.Cards.Add(fizz);

        var bellowsCard = new RiftboundCard
        {
            Id = 400_001,
            Name = "Bellows Breath",
            Type = "Spell",
            Cost = 1,
            Power = 1,
            Color = ["Mind"],
            Effect = "[Action] (Play on your turn or in showdowns.) [Repeat] :rb_energy_1::rb_rune_mind: (You may pay the additional cost to repeat this spell's effect.) Deal 1 to up to three units at the same location.",
        };
        var bellows = BuildCardInstance(bellowsCard, ownerPlayer: 0, controllerPlayer: 0);
        player.TrashZone.Cards.Add(bellows);

        var baseUnitA = BuildUnit(ownerPlayer: 1, controllerPlayer: 1, name: "Base Unit A", might: 1);
        var baseUnitB = BuildUnit(ownerPlayer: 1, controllerPlayer: 1, name: "Base Unit B", might: 1);
        var battlefieldUnit = BuildUnit(
            ownerPlayer: 1,
            controllerPlayer: 1,
            name: "Battlefield Unit",
            might: 1
        );
        opponent.BaseZone.Cards.Add(baseUnitA);
        opponent.BaseZone.Cards.Add(baseUnitB);
        session.Battlefields[1].Units.Add(battlefieldUnit);

        var firstChaosRuneAction = engine
            .GetLegalActions(session)
            .First(a => a.ActionId.Contains(chaosRunes[0].InstanceId.ToString(), StringComparison.Ordinal))
            .ActionId;
        var secondChaosRuneAction = engine
            .GetLegalActions(session)
            .First(a => a.ActionId.Contains(chaosRunes[1].InstanceId.ToString(), StringComparison.Ordinal))
            .ActionId;
        var firstMindRuneAction = engine
            .GetLegalActions(session)
            .First(a => a.ActionId.Contains(mindRunes[0].InstanceId.ToString(), StringComparison.Ordinal))
            .ActionId;
        var sealAction = engine
            .GetLegalActions(session)
            .First(a => a.ActionId.Contains(sealA.InstanceId.ToString(), StringComparison.Ordinal))
            .ActionId;

        Assert.True(engine.ApplyAction(session, firstChaosRuneAction).Succeeded);
        Assert.True(engine.ApplyAction(session, secondChaosRuneAction).Succeeded);
        Assert.True(engine.ApplyAction(session, firstMindRuneAction).Succeeded);
        Assert.True(engine.ApplyAction(session, sealAction).Succeeded);
        Assert.Equal(3, player.RunePool.Energy);
        Assert.Equal(1, ReadPower(player, "Chaos"));
        Assert.Equal(0, ReadPower(player, "Mind"));
        Assert.True(sealA.IsExhausted);
        Assert.False(sealB.IsExhausted);

        var playFizzAction = engine
            .GetLegalActions(session)
            .First(a =>
                a.ActionType == RiftboundActionType.PlayCard
                && a.ActionId.Contains(fizz.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.EndsWith("-to-base", StringComparison.Ordinal)
            )
            .ActionId;
        var playFizzResult = engine.ApplyAction(session, playFizzAction);
        Assert.True(playFizzResult.Succeeded, playFizzResult.ErrorMessage);
        Assert.Equal(RiftboundTurnState.NeutralClosed, session.State);
        Assert.Equal(0, player.RunePool.Energy);
        Assert.Equal(0, ReadPower(player, "Chaos"));
        Assert.Equal(0, ReadPower(player, "Mind"));
        Assert.Equal(9, player.BaseZone.Cards.Count(c => string.Equals(c.Type, "Rune", StringComparison.OrdinalIgnoreCase)));
        Assert.Contains(player.BaseZone.Cards, c => c.InstanceId == fizz.InstanceId);
        Assert.DoesNotContain(player.TrashZone.Cards, c => c.InstanceId == bellows.InstanceId);
        Assert.Contains(player.MainDeckZone.Cards, c => c.InstanceId == bellows.InstanceId);

        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);
        var secondPass = engine.ApplyAction(session, "pass-focus");
        Assert.True(secondPass.Succeeded, secondPass.ErrorMessage);

        Assert.Equal(RiftboundTurnState.NeutralOpen, session.State);
        Assert.Empty(session.Chain);
        Assert.DoesNotContain(opponent.BaseZone.Cards, c => c.InstanceId == baseUnitA.InstanceId);
        Assert.DoesNotContain(opponent.BaseZone.Cards, c => c.InstanceId == baseUnitB.InstanceId);
        Assert.Contains(session.Battlefields[1].Units, c => c.InstanceId == battlefieldUnit.InstanceId);
        Assert.Contains(opponent.TrashZone.Cards, c => c.InstanceId == baseUnitA.InstanceId);
        Assert.Contains(opponent.TrashZone.Cards, c => c.InstanceId == baseUnitB.InstanceId);
        Assert.DoesNotContain(opponent.TrashZone.Cards, c => c.InstanceId == battlefieldUnit.InstanceId);

        Assert.Contains(
            session.EffectContexts,
            c => c.Source == "Seal of Discord" && c.Timing == "Activate"
        );
        Assert.Contains(
            session.EffectContexts,
            c => c.Source == "Fizz - Trickster" && c.Timing == "WhenPlay"
        );
        Assert.Single(
            session.EffectContexts,
            c =>
                c.Source == "Bellows Breath"
                && c.Metadata.TryGetValue("repeat", out var repeatFlag)
                && repeatFlag == "false"
        );
        Assert.DoesNotContain(
            session.EffectContexts,
            c =>
                c.Source == "Bellows Breath"
                && c.Metadata.TryGetValue("repeat", out var repeatFlag)
                && repeatFlag == "true"
        );
    }
}

