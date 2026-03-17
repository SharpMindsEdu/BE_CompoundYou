using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Unit.Tests.Features.Riftbound.Simulation;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.RiftboundTests)]
public sealed class RiftboundSimulationEnginePlayCardBehaviorTests : RiftboundSimulationEngineBehaviorTestBase
{
    [Fact]
    public void PlayCard_WithMulticolorPower_CanRecycleEitherColorRune()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031601,
                RiftboundSimulationTestData.BuildDeck(9300, "Chaos"),
                RiftboundSimulationTestData.BuildDeck(9400, "Order")
            )
        );

        var player = session.Players[0];
        player.HandZone.Cards.Clear();
        player.BaseZone.Cards.Clear();
        player.MainDeckZone.Cards.Clear();
        player.TrashZone.Cards.Clear();

        var furyRune = BuildRuneInstance(501_001, "Fury Rune", "Fury", ownerPlayer: 0);
        var mindRune = BuildRuneInstance(501_002, "Mind Rune", "Mind", ownerPlayer: 0);
        player.BaseZone.Cards.Add(furyRune);
        player.BaseZone.Cards.Add(mindRune);

        var multicolorSpell = new RiftboundCard
        {
            Id = 700_001,
            Name = "Split Domain Spell",
            Type = "Spell",
            Cost = 0,
            Power = 2,
            Color = ["Fury", "Mind"],
        };
        var spellInstance = BuildCardInstance(multicolorSpell, ownerPlayer: 0, controllerPlayer: 0);
        player.HandZone.Cards.Add(spellInstance);
        var runeDeckCountBefore = player.RuneDeckZone.Cards.Count;
        var baseRuneCountBefore = player.BaseZone.Cards.Count(c => string.Equals(c.Type, "Rune", StringComparison.OrdinalIgnoreCase));

        var playAction = engine
            .GetLegalActions(session)
            .First(a =>
                a.ActionType == RiftboundActionType.PlayCard
                && a.ActionId.Contains(spellInstance.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.EndsWith("-spell", StringComparison.Ordinal)
            )
            .ActionId;

        var result = engine.ApplyAction(session, playAction);

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Equal(baseRuneCountBefore - 2, player.BaseZone.Cards.Count(c => string.Equals(c.Type, "Rune", StringComparison.OrdinalIgnoreCase)));
        Assert.Equal(runeDeckCountBefore + 2, player.RuneDeckZone.Cards.Count);
    }

        [Fact]
    public void PlayCard_WithMulticolorPower_NotEnoughRunes()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031601,
                RiftboundSimulationTestData.BuildDeck(9300, "Chaos"),
                RiftboundSimulationTestData.BuildDeck(9400, "Order")
            )
        );

        var player = session.Players[0];
        player.HandZone.Cards.Clear();
        player.BaseZone.Cards.Clear();
        player.MainDeckZone.Cards.Clear();
        player.TrashZone.Cards.Clear();

        var furyRune = BuildRuneInstance(501_001, "Fury Rune", "Fury", ownerPlayer: 0);
        player.BaseZone.Cards.Add(furyRune);

        var multicolorSpell = new RiftboundCard
        {
            Id = 700_001,
            Name = "Split Domain Spell",
            Type = "Spell",
            Cost = 0,
            Power = 2,
            Color = ["Fury", "Mind"],
        };
        var spellInstance = BuildCardInstance(multicolorSpell, ownerPlayer: 0, controllerPlayer: 0);
        player.HandZone.Cards.Add(spellInstance);
        
        Assert.DoesNotContain(spellInstance.InstanceId.ToString(), engine.GetLegalActions(session).Select(x => x.ActionId));
    }

    [Fact]
    public void PlayCard_WithPower_CorrectRunes()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031601,
                RiftboundSimulationTestData.BuildDeck(9300, "Chaos"),
                RiftboundSimulationTestData.BuildDeck(9400, "Order")
            )
        );

        var player = session.Players[0];
        player.HandZone.Cards.Clear();
        player.BaseZone.Cards.Clear();
        player.MainDeckZone.Cards.Clear();
        player.TrashZone.Cards.Clear();

        var furyRune = BuildRuneInstance(501_001, "Fury Rune", "Fury", ownerPlayer: 0);
        player.BaseZone.Cards.Add(furyRune);

        var multicolorSpell = new RiftboundCard
        {
            Id = 700_001,
            Name = "Split Domain Spell",
            Type = "Spell",
            Cost = 0,
            Power = 1,
            Color = ["Fury"],
        };
        var spellInstance = BuildCardInstance(multicolorSpell, ownerPlayer: 0, controllerPlayer: 0);
        player.HandZone.Cards.Add(spellInstance);
        
        Assert.DoesNotContain(spellInstance.InstanceId.ToString(), engine.GetLegalActions(session).Select(x => x.ActionId));
    }

    [Fact]
    public void PlayCard_WithPower_WrongRunes()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031601,
                RiftboundSimulationTestData.BuildDeck(9300, "Chaos"),
                RiftboundSimulationTestData.BuildDeck(9400, "Order")
            )
        );

        var player = session.Players[0];
        player.HandZone.Cards.Clear();
        player.BaseZone.Cards.Clear();
        player.MainDeckZone.Cards.Clear();
        player.TrashZone.Cards.Clear();

        var furyRune = BuildRuneInstance(501_001, "Fury Rune", "Fury", ownerPlayer: 0);
        player.BaseZone.Cards.Add(furyRune);

        var multicolorSpell = new RiftboundCard
        {
            Id = 700_001,
            Name = "Split Domain Spell",
            Type = "Spell",
            Cost = 0,
            Power = 1,
            Color = ["Mind"],
        };
        var spellInstance = BuildCardInstance(multicolorSpell, ownerPlayer: 0, controllerPlayer: 0);
        player.HandZone.Cards.Add(spellInstance);
        
        Assert.DoesNotContain(spellInstance.InstanceId.ToString(), engine.GetLegalActions(session).Select(x => x.ActionId));
    }

    [Fact]
    public void PlayCard_WithHiddenPower_CanRecycleAnyRune()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031602,
                RiftboundSimulationTestData.BuildDeck(9500, "Chaos"),
                RiftboundSimulationTestData.BuildDeck(9600, "Order")
            )
        );

        var player = session.Players[0];
        player.HandZone.Cards.Clear();
        player.BaseZone.Cards.Clear();
        player.MainDeckZone.Cards.Clear();
        player.TrashZone.Cards.Clear();

        player.BaseZone.Cards.Add(BuildRuneInstance(601_001, "Chaos Rune", "Chaos", ownerPlayer: 0));
        player.BaseZone.Cards.Add(BuildRuneInstance(601_002, "Order Rune", "Order", ownerPlayer: 0));

        var hiddenSpell = new RiftboundCard
        {
            Id = 800_001,
            Name = "Hidden Technique",
            Type = "Spell",
            Cost = 0,
            Power = 2,
            Color = ["Mind"],
            Tags = ["Hidden"],
        };
        var spellInstance = BuildCardInstance(hiddenSpell, ownerPlayer: 0, controllerPlayer: 0);
        player.HandZone.Cards.Add(spellInstance);
        var runeDeckCountBefore = player.RuneDeckZone.Cards.Count;
        var baseRuneCountBefore = player.BaseZone.Cards.Count(c => string.Equals(c.Type, "Rune", StringComparison.OrdinalIgnoreCase));

        var playAction = engine
            .GetLegalActions(session)
            .First(a =>
                a.ActionType == RiftboundActionType.PlayCard
                && a.ActionId.Contains(spellInstance.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.EndsWith("-spell", StringComparison.Ordinal)
            )
            .ActionId;

        var result = engine.ApplyAction(session, playAction);

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Equal(baseRuneCountBefore - 2, player.BaseZone.Cards.Count(c => string.Equals(c.Type, "Rune", StringComparison.OrdinalIgnoreCase)));
        Assert.Equal(runeDeckCountBefore + 2, player.RuneDeckZone.Cards.Count);
    }
}

