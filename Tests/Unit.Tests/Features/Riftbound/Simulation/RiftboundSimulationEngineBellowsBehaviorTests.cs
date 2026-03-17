using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Unit.Tests.Features.Riftbound.Simulation;

[Trait("category", ServiceTestCategories.UnitTests)]
public sealed class RiftboundSimulationEngineBellowsBehaviorTests : RiftboundSimulationEngineBehaviorTestBase
{
    [Fact]
    public void Bellows_WithExplicitRepeatAction_AppliesRepeat()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031606,
                RiftboundSimulationTestData.BuildDeck(9940, "Chaos"),
                RiftboundSimulationTestData.BuildDeck(9950, "Order")
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

        var mindRuneA = BuildRuneInstance(203_100, "Mind Rune", "Mind", ownerPlayer: 0);
        var mindRuneB = BuildRuneInstance(203_101, "Mind Rune", "Mind", ownerPlayer: 0);
        player.BaseZone.Cards.Add(mindRuneA);
        player.BaseZone.Cards.Add(mindRuneB);

        var bellows = BuildCardInstance(
            new RiftboundCard
            {
                Id = 403_100,
                Name = "Bellows Breath",
                Type = "Spell",
                Cost = 1,
                Power = 1,
                Color = ["Mind"],
                Effect = "[Action] (Play on your turn or in showdowns.) [Repeat] :rb_energy_1::rb_rune_mind: (You may pay the additional cost to repeat this spell's effect.) Deal 1 to up to three units at the same location.",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.HandZone.Cards.Add(bellows);

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

        var firstRuneAction = engine
            .GetLegalActions(session)
            .First(a => a.ActionId.Contains(mindRuneA.InstanceId.ToString(), StringComparison.Ordinal))
            .ActionId;
        var secondRuneAction = engine
            .GetLegalActions(session)
            .First(a => a.ActionId.Contains(mindRuneB.InstanceId.ToString(), StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, firstRuneAction).Succeeded);
        Assert.True(engine.ApplyAction(session, secondRuneAction).Succeeded);

        var legalActions = engine.GetLegalActions(session);
        var nonRepeatAction = legalActions
            .Single(a =>
                a.ActionType == RiftboundActionType.PlayCard
                && a.ActionId.Contains(bellows.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.Contains("-target-units-", StringComparison.Ordinal)
                && !a.ActionId.EndsWith("-repeat", StringComparison.Ordinal)
                && a.ActionId.Contains(baseUnitA.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.Contains(baseUnitB.InstanceId.ToString(), StringComparison.Ordinal)
                && !a.ActionId.Contains(battlefieldUnit.InstanceId.ToString(), StringComparison.Ordinal)
            )
            .ActionId;
        var repeatAction = legalActions
            .Single(a =>
                a.ActionType == RiftboundActionType.PlayCard
                && a.ActionId.Contains(bellows.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.Contains("-target-units-", StringComparison.Ordinal)
                && a.ActionId.EndsWith("-repeat", StringComparison.Ordinal)
                && a.ActionId.Contains(baseUnitA.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.Contains(baseUnitB.InstanceId.ToString(), StringComparison.Ordinal)
                && !a.ActionId.Contains(battlefieldUnit.InstanceId.ToString(), StringComparison.Ordinal)
            )
            .ActionId;
        Assert.NotEqual(nonRepeatAction, repeatAction);

        Assert.True(engine.ApplyAction(session, repeatAction).Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);

        Assert.DoesNotContain(opponent.BaseZone.Cards, c => c.InstanceId == baseUnitA.InstanceId);
        Assert.DoesNotContain(opponent.BaseZone.Cards, c => c.InstanceId == baseUnitB.InstanceId);
        Assert.Contains(session.Battlefields[1].Units, c => c.InstanceId == battlefieldUnit.InstanceId);

        Assert.Contains(
            session.EffectContexts,
            c =>
                c.Source == "Bellows Breath"
                && c.Metadata.TryGetValue("repeat", out var repeat)
                && repeat == "true"
        );
    }

    [Fact]
    public void Bellows_TargetSelection_ChoosesExactUnitsAndKeepsSameLocation()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031607,
                RiftboundSimulationTestData.BuildDeck(9960, "Chaos"),
                RiftboundSimulationTestData.BuildDeck(9970, "Order")
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

        var mindRune = BuildRuneInstance(204_100, "Mind Rune", "Mind", ownerPlayer: 0);
        player.BaseZone.Cards.Add(mindRune);

        var bellows = BuildCardInstance(
            new RiftboundCard
            {
                Id = 404_100,
                Name = "Bellows Breath",
                Type = "Spell",
                Cost = 1,
                Power = 1,
                Color = ["Mind"],
                Effect = "[Action] (Play on your turn or in showdowns.) [Repeat] :rb_energy_1::rb_rune_mind: (You may pay the additional cost to repeat this spell's effect.) Deal 1 to up to three units at the same location.",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.HandZone.Cards.Add(bellows);

        var baseUnitA = BuildUnit(ownerPlayer: 1, controllerPlayer: 1, name: "Base Unit A", might: 2);
        var baseUnitB = BuildUnit(ownerPlayer: 1, controllerPlayer: 1, name: "Base Unit B", might: 2);
        var baseUnitC = BuildUnit(ownerPlayer: 1, controllerPlayer: 1, name: "Base Unit C", might: 3);
        var baseUnitD = BuildUnit(ownerPlayer: 1, controllerPlayer: 1, name: "Base Unit D", might: 3);
        var battlefieldUnit = BuildUnit(
            ownerPlayer: 1,
            controllerPlayer: 1,
            name: "Battlefield Unit",
            might: 3
        );
        opponent.BaseZone.Cards.Add(baseUnitA);
        opponent.BaseZone.Cards.Add(baseUnitB);
        opponent.BaseZone.Cards.Add(baseUnitC);
        opponent.BaseZone.Cards.Add(baseUnitD);
        session.Battlefields[1].Units.Add(battlefieldUnit);

        var activateRuneAction = engine
            .GetLegalActions(session)
            .First(a => a.ActionId.Contains(mindRune.InstanceId.ToString(), StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, activateRuneAction).Succeeded);

        var bellowsActions = engine
            .GetLegalActions(session)
            .Where(a =>
                a.ActionType == RiftboundActionType.PlayCard
                && a.ActionId.Contains(bellows.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.Contains("-target-units-", StringComparison.Ordinal)
                && !a.ActionId.EndsWith("-repeat", StringComparison.Ordinal)
            )
            .ToList();
        Assert.NotEmpty(bellowsActions);
        Assert.DoesNotContain(
            bellowsActions,
            a =>
                a.ActionId.Contains(baseUnitA.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.Contains(battlefieldUnit.InstanceId.ToString(), StringComparison.Ordinal)
        );

        var chosenAction = bellowsActions
            .Single(a =>
                a.ActionId.Contains(baseUnitA.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.Contains(baseUnitB.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.Contains(baseUnitC.InstanceId.ToString(), StringComparison.Ordinal)
                && !a.ActionId.Contains(baseUnitD.InstanceId.ToString(), StringComparison.Ordinal)
                && !a.ActionId.Contains(battlefieldUnit.InstanceId.ToString(), StringComparison.Ordinal)
            )
            .ActionId;

        Assert.True(engine.ApplyAction(session, chosenAction).Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);

        Assert.Equal(1, baseUnitA.MarkedDamage);
        Assert.Equal(1, baseUnitB.MarkedDamage);
        Assert.Equal(1, baseUnitC.MarkedDamage);
        Assert.Equal(0, baseUnitD.MarkedDamage);
        Assert.Equal(0, battlefieldUnit.MarkedDamage);
        Assert.Equal(4, opponent.BaseZone.Cards.Count(c => string.Equals(c.Type, "Unit", StringComparison.OrdinalIgnoreCase)));
        Assert.Contains(session.Battlefields[1].Units, c => c.InstanceId == battlefieldUnit.InstanceId);

        var bellowsContexts = session
            .EffectContexts.Where(c => c.Source == "Bellows Breath")
            .Where(c => c.Metadata.TryGetValue("repeat", out var repeat) && repeat == "false")
            .ToList();
        Assert.Single(bellowsContexts);
        Assert.Equal("base-1", bellowsContexts[0].Metadata["location"]);
        Assert.Equal("3", bellowsContexts[0].Metadata["targets"]);
    }
}

