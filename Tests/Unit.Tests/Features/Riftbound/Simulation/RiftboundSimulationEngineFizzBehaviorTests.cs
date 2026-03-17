using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Unit.Tests.Features.Riftbound.Simulation;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.RiftboundTests)]
public sealed class RiftboundSimulationEngineFizzBehaviorTests : RiftboundSimulationEngineBehaviorTestBase
{
    [Fact]
    public void Fizz_BellowsWithoutRepeat_TargetsOnlyBaseUnits()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031603,
                RiftboundSimulationTestData.BuildDeck(9700, "Chaos"),
                RiftboundSimulationTestData.BuildDeck(9800, "Order")
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
            .Range(0, 3)
            .Select(i => BuildRuneInstance(200_100 + i, "Chaos Rune", "Chaos", ownerPlayer: 0))
            .ToList();
        var mindRune = BuildRuneInstance(200_200, "Mind Rune", "Mind", ownerPlayer: 0);
        player.BaseZone.Cards.AddRange(chaosRunes);
        player.BaseZone.Cards.Add(mindRune);

        var fizz = BuildCardInstance(
            new RiftboundCard
            {
                Id = 300_100,
                Name = "Fizz - Trickster",
                Type = "Unit",
                Supertype = "Champion",
                Cost = 3,
                Power = 1,
                Might = 2,
                Color = ["Chaos"],
                Effect = "When you play me, you may play a spell from your trash with Energy cost no more than :rb_energy_3:, ignoring its Energy cost. Recycle that spell after you play it. (You must still pay its Power cost.)",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.HandZone.Cards.Add(fizz);

        var bellows = BuildCardInstance(
            new RiftboundCard
            {
                Id = 400_100,
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

        foreach (var rune in chaosRunes)
        {
            var activateAction = engine
                .GetLegalActions(session)
                .First(a => a.ActionId.Contains(rune.InstanceId.ToString(), StringComparison.Ordinal))
                .ActionId;
            Assert.True(engine.ApplyAction(session, activateAction).Succeeded);
        }

        var playFizzAction = engine
            .GetLegalActions(session)
            .First(a =>
                a.ActionType == RiftboundActionType.PlayCard
                && a.ActionId.Contains(fizz.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.EndsWith("-to-base", StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, playFizzAction).Succeeded);

        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);

        Assert.DoesNotContain(opponent.BaseZone.Cards, c => c.InstanceId == baseUnitA.InstanceId);
        Assert.DoesNotContain(opponent.BaseZone.Cards, c => c.InstanceId == baseUnitB.InstanceId);
        Assert.Contains(session.Battlefields[1].Units, c => c.InstanceId == battlefieldUnit.InstanceId);
        Assert.Contains(opponent.TrashZone.Cards, c => c.InstanceId == baseUnitA.InstanceId);
        Assert.Contains(opponent.TrashZone.Cards, c => c.InstanceId == baseUnitB.InstanceId);
        Assert.DoesNotContain(opponent.TrashZone.Cards, c => c.InstanceId == battlefieldUnit.InstanceId);

        var bellowsContexts = session
            .EffectContexts.Where(c => c.Source == "Bellows Breath")
            .Where(c => c.Metadata.TryGetValue("repeat", out var repeat) && repeat == "false")
            .ToList();
        Assert.Single(bellowsContexts);
        Assert.Equal("base-1", bellowsContexts[0].Metadata["location"]);
        Assert.DoesNotContain(
            session.EffectContexts,
            c =>
                c.Source == "Bellows Breath"
                && c.Metadata.TryGetValue("repeat", out var repeat)
                && repeat == "true"
        );
    }

    [Fact]
    public void Fizz_BellowsWithoutRepeat_TargetsOnlyBattlefieldUnit()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031604,
                RiftboundSimulationTestData.BuildDeck(9900, "Chaos"),
                RiftboundSimulationTestData.BuildDeck(9910, "Order")
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
            .Range(0, 3)
            .Select(i => BuildRuneInstance(201_100 + i, "Chaos Rune", "Chaos", ownerPlayer: 0))
            .ToList();
        var mindRune = BuildRuneInstance(201_200, "Mind Rune", "Mind", ownerPlayer: 0);
        player.BaseZone.Cards.AddRange(chaosRunes);
        player.BaseZone.Cards.Add(mindRune);

        var fizz = BuildCardInstance(
            new RiftboundCard
            {
                Id = 301_100,
                Name = "Fizz - Trickster",
                Type = "Unit",
                Supertype = "Champion",
                Cost = 3,
                Power = 1,
                Might = 2,
                Color = ["Chaos"],
                Effect = "When you play me, you may play a spell from your trash with Energy cost no more than :rb_energy_3:, ignoring its Energy cost. Recycle that spell after you play it. (You must still pay its Power cost.)",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.HandZone.Cards.Add(fizz);

        var bellows = BuildCardInstance(
            new RiftboundCard
            {
                Id = 401_100,
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
        player.TrashZone.Cards.Add(bellows);

        var battlefieldUnit = BuildUnit(
            ownerPlayer: 1,
            controllerPlayer: 1,
            name: "Battlefield Unit",
            might: 1
        );
        session.Battlefields[1].Units.Add(battlefieldUnit);

        foreach (var rune in chaosRunes)
        {
            var activateAction = engine
                .GetLegalActions(session)
                .First(a => a.ActionId.Contains(rune.InstanceId.ToString(), StringComparison.Ordinal))
                .ActionId;
            Assert.True(engine.ApplyAction(session, activateAction).Succeeded);
        }

        var playFizzAction = engine
            .GetLegalActions(session)
            .First(a =>
                a.ActionType == RiftboundActionType.PlayCard
                && a.ActionId.Contains(fizz.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.EndsWith("-to-base", StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, playFizzAction).Succeeded);

        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);

        Assert.DoesNotContain(session.Battlefields[1].Units, c => c.InstanceId == battlefieldUnit.InstanceId);
        Assert.Contains(opponent.TrashZone.Cards, c => c.InstanceId == battlefieldUnit.InstanceId);
        Assert.DoesNotContain(
            opponent.BaseZone.Cards,
            c => string.Equals(c.Type, "Unit", StringComparison.OrdinalIgnoreCase)
        );

        var bellowsContexts = session
            .EffectContexts.Where(c => c.Source == "Bellows Breath")
            .Where(c => c.Metadata.TryGetValue("repeat", out var repeat) && repeat == "false")
            .ToList();
        Assert.Single(bellowsContexts);
        Assert.Equal("bf-1", bellowsContexts[0].Metadata["location"]);
        Assert.DoesNotContain(
            session.EffectContexts,
            c =>
                c.Source == "Bellows Breath"
                && c.Metadata.TryGetValue("repeat", out var repeat)
                && repeat == "true"
        );
    }

    [Fact]
    public void Fizz_WithSecondTrashSpell_PlaysStackedDeckInsteadOfBellows()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031605,
                RiftboundSimulationTestData.BuildDeck(9920, "Chaos"),
                RiftboundSimulationTestData.BuildDeck(9930, "Order")
            )
        );

        var player = session.Players[0];
        player.HandZone.Cards.Clear();
        player.BaseZone.Cards.Clear();
        player.TrashZone.Cards.Clear();
        player.MainDeckZone.Cards.Clear();
        player.RunePool.Energy = 0;
        player.RunePool.PowerByDomain.Clear();

        var chaosRunes = Enumerable
            .Range(0, 4)
            .Select(i => BuildRuneInstance(202_100 + i, "Chaos Rune", "Chaos", ownerPlayer: 0))
            .ToList();
        var mindRune = BuildRuneInstance(202_200, "Mind Rune", "Mind", ownerPlayer: 0);
        player.BaseZone.Cards.AddRange(chaosRunes);
        player.BaseZone.Cards.Add(mindRune);

        var fizz = BuildCardInstance(
            new RiftboundCard
            {
                Id = 302_100,
                Name = "Fizz - Trickster",
                Type = "Unit",
                Supertype = "Champion",
                Cost = 3,
                Power = 1,
                Might = 2,
                Color = ["Chaos"],
                Effect = "When you play me, you may play a spell from your trash with Energy cost no more than :rb_energy_3:, ignoring its Energy cost. Recycle that spell after you play it. (You must still pay its Power cost.)",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.HandZone.Cards.Add(fizz);

        var bellows = BuildCardInstance(
            new RiftboundCard
            {
                Id = 402_100,
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
        var stackedDeck = BuildCardInstance(
            new RiftboundCard
            {
                Id = 402_200,
                Name = "Stacked Deck",
                Type = "Spell",
                Cost = 1,
                Power = 0,
                Color = ["Mind"],
                Effect = "[Action] (play on your turn or in showdowns.) \nLook at the top 3 cards of your Main Deck. Put 1 into your hand and recycle the rest.",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.TrashZone.Cards.Add(bellows);
        player.TrashZone.Cards.Add(stackedDeck);

        foreach (var rune in chaosRunes.Take(3))
        {
            var activateAction = engine
                .GetLegalActions(session)
                .First(a => a.ActionId.Contains(rune.InstanceId.ToString(), StringComparison.Ordinal))
                .ActionId;
            Assert.True(engine.ApplyAction(session, activateAction).Succeeded);
        }

        var playFizzAction = engine
            .GetLegalActions(session)
            .First(a =>
                a.ActionType == RiftboundActionType.PlayCard
                && a.ActionId.Contains(fizz.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.EndsWith("-to-base", StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, playFizzAction).Succeeded);

        var fizzWhenPlay = session
            .EffectContexts.Where(c => c.Source == "Fizz - Trickster" && c.Timing == "WhenPlay")
            .ToList();
        Assert.Single(fizzWhenPlay);
        Assert.True(fizzWhenPlay[0].Metadata.TryGetValue("playedSpell", out var playedSpell));
        Assert.Equal("Stacked Deck", playedSpell);

        Assert.Contains(player.TrashZone.Cards, c => c.InstanceId == bellows.InstanceId);
        Assert.DoesNotContain(player.TrashZone.Cards, c => c.InstanceId == stackedDeck.InstanceId);
        Assert.Contains(player.MainDeckZone.Cards, c => c.InstanceId == stackedDeck.InstanceId);
        Assert.DoesNotContain(
            session.EffectContexts,
            c => c.Source == "Bellows Breath" && c.Timing == "Resolve"
        );
    }
}

