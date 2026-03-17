using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Unit.Tests.Features.Riftbound.Simulation;

[Trait("category", ServiceTestCategories.UnitTests)]
public sealed class RiftboundSimulationEngineUndertitanBehaviorTests : RiftboundSimulationEngineBehaviorTestBase
{
    [Fact]
    public void Undertitan_RevealedByStackedDeck_AddsTwoEnergy()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031616,
                RiftboundSimulationTestData.BuildDeck(9981, "Order"),
                RiftboundSimulationTestData.BuildDeck(9982, "Chaos")
            )
        );

        var player = session.Players[0];
        player.HandZone.Cards.Clear();
        player.BaseZone.Cards.Clear();
        player.MainDeckZone.Cards.Clear();
        player.TrashZone.Cards.Clear();
        player.RunePool.Energy = 0;
        player.RunePool.PowerByDomain.Clear();

        var orderRune = BuildRuneInstance(210_100, "Order Rune", "Order", ownerPlayer: 0);
        player.BaseZone.Cards.Add(orderRune);

        var stackedDeck = BuildCardInstance(
            new RiftboundCard
            {
                Id = 410_100,
                Name = "Stacked Deck",
                Type = "Spell",
                Cost = 1,
                Power = 0,
                Color = ["Chaos"],
                GameplayKeywords = ["Action"],
                Effect = "[Action] Look at the top 3 cards of your Main Deck. Put 1 into your hand and recycle the rest.",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.HandZone.Cards.Add(stackedDeck);

        var undertitan = BuildCardInstance(
            new RiftboundCard
            {
                Id = 410_200,
                Name = "Undertitan",
                Type = "Unit",
                Cost = 6,
                Power = 1,
                Might = 5,
                Color = ["Order"],
                Effect = "When you play me, give your other units +2 [Might] this turn. As I'm revealed from your deck, [Add] [2].",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        var fillerA = BuildCardInstance(
            new RiftboundCard
            {
                Id = 410_201,
                Name = "Filler X",
                Type = "Unit",
                Cost = 1,
                Power = 0,
                Might = 1,
                Color = ["Order"],
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        var fillerB = BuildCardInstance(
            new RiftboundCard
            {
                Id = 410_202,
                Name = "Filler Y",
                Type = "Unit",
                Cost = 1,
                Power = 0,
                Might = 1,
                Color = ["Order"],
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.MainDeckZone.Cards.Add(undertitan);
        player.MainDeckZone.Cards.Add(fillerA);
        player.MainDeckZone.Cards.Add(fillerB);

        var activateRuneAction = engine
            .GetLegalActions(session)
            .First(a => a.ActionId.Contains(orderRune.InstanceId.ToString(), StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, activateRuneAction).Succeeded);

        var castAction = engine
            .GetLegalActions(session)
            .First(a =>
                a.ActionType == RiftboundActionType.PlayCard
                && a.ActionId.Contains(stackedDeck.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.EndsWith("-spell", StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, castAction).Succeeded);

        Assert.Equal(2, player.RunePool.Energy);
        Assert.DoesNotContain(session.Battlefields.SelectMany(x => x.Units), x => x.InstanceId == undertitan.InstanceId);
        Assert.Contains(
            session.EffectContexts,
            c =>
                c.Source == "Undertitan"
                && c.Timing == "Reveal"
                && c.Metadata.TryGetValue("addEnergy", out var added)
                && added == "2"
                && c.Metadata.TryGetValue("sourceCard", out var source)
                && source == "Stacked Deck"
        );
    }

    [Fact]
    public void Undertitan_RevealedByCalledShot_AddsTwoEnergy()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031617,
                RiftboundSimulationTestData.BuildDeck(9983, "Chaos"),
                RiftboundSimulationTestData.BuildDeck(9984, "Order")
            )
        );

        var player = session.Players[0];
        player.HandZone.Cards.Clear();
        player.BaseZone.Cards.Clear();
        player.MainDeckZone.Cards.Clear();
        player.TrashZone.Cards.Clear();
        player.RunePool.Energy = 0;
        player.RunePool.PowerByDomain.Clear();

        player.BaseZone.Cards.Add(BuildRuneInstance(211_100, "Chaos Rune", "Chaos", ownerPlayer: 0));

        var calledShot = BuildCardInstance(
            new RiftboundCard
            {
                Id = 411_100,
                Name = "Called Shot",
                Type = "Spell",
                Cost = 0,
                Power = 1,
                Color = ["Chaos"],
                GameplayKeywords = ["Action", "Repeat"],
                Effect = "[ACTION] [REPEAT] [CHAOS] Look at the top 2 cards of your Main Deck. Draw one and recycle the other.",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.HandZone.Cards.Add(calledShot);

        var undertitan = BuildCardInstance(
            new RiftboundCard
            {
                Id = 411_200,
                Name = "Undertitan",
                Type = "Unit",
                Cost = 6,
                Power = 1,
                Might = 5,
                Color = ["Order"],
                Effect = "When you play me, give your other units +2 [Might] this turn. As I'm revealed from your deck, [Add] [2].",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        var filler = BuildCardInstance(
            new RiftboundCard
            {
                Id = 411_201,
                Name = "Filler Z",
                Type = "Unit",
                Cost = 1,
                Power = 0,
                Might = 1,
                Color = ["Chaos"],
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.MainDeckZone.Cards.Add(undertitan);
        player.MainDeckZone.Cards.Add(filler);

        var castAction = engine
            .GetLegalActions(session)
            .First(a =>
                a.ActionType == RiftboundActionType.PlayCard
                && a.ActionId.Contains(calledShot.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.EndsWith("-spell", StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, castAction).Succeeded);

        Assert.Equal(2, player.RunePool.Energy);
        Assert.Contains(
            session.EffectContexts,
            c =>
                c.Source == "Undertitan"
                && c.Timing == "Reveal"
                && c.Metadata.TryGetValue("addEnergy", out var added)
                && added == "2"
                && c.Metadata.TryGetValue("sourceCard", out var source)
                && source == "Called Shot"
        );
    }

    [Fact]
    public void Undertitan_OnPlay_BuffsOtherFriendlyUnitsByTwo()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031618,
                RiftboundSimulationTestData.BuildDeck(9985, "Order"),
                RiftboundSimulationTestData.BuildDeck(9986, "Chaos")
            )
        );

        var player = session.Players[0];
        player.HandZone.Cards.Clear();
        player.BaseZone.Cards.Clear();
        player.MainDeckZone.Cards.Clear();
        player.TrashZone.Cards.Clear();
        player.RunePool.Energy = 0;
        player.RunePool.PowerByDomain.Clear();

        var runes = Enumerable
            .Range(0, 6)
            .Select(i => BuildRuneInstance(212_100 + i, "Order Rune", "Order", ownerPlayer: 0))
            .ToList();
        player.BaseZone.Cards.AddRange(runes);

        var undertitan = BuildCardInstance(
            new RiftboundCard
            {
                Id = 412_100,
                Name = "Undertitan",
                Type = "Unit",
                Cost = 6,
                Power = 1,
                Might = 5,
                Color = ["Order"],
                Effect = "When you play me, give your other units +2 [Might] this turn. As I'm revealed from your deck, [Add] [2].",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.HandZone.Cards.Add(undertitan);

        var friendlyBase = BuildUnit(ownerPlayer: 0, controllerPlayer: 0, name: "Base Buddy", might: 2);
        var friendlyBattlefield = BuildUnit(ownerPlayer: 0, controllerPlayer: 0, name: "Battle Buddy", might: 2);
        player.BaseZone.Cards.Add(friendlyBase);
        session.Battlefields[0].Units.Add(friendlyBattlefield);

        foreach (var rune in runes)
        {
            var activateAction = engine
                .GetLegalActions(session)
                .First(a => a.ActionId.Contains(rune.InstanceId.ToString(), StringComparison.Ordinal))
                .ActionId;
            Assert.True(engine.ApplyAction(session, activateAction).Succeeded);
        }

        var playAction = engine
            .GetLegalActions(session)
            .First(a =>
                a.ActionType == RiftboundActionType.PlayCard
                && a.ActionId.Contains(undertitan.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.EndsWith("-to-base", StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, playAction).Succeeded);

        Assert.Equal(2, friendlyBase.TemporaryMightModifier);
        Assert.Equal(2, friendlyBattlefield.TemporaryMightModifier);
        Assert.Equal(0, undertitan.TemporaryMightModifier);
        Assert.Contains(
            session.EffectContexts,
            c =>
                c.Source == "Undertitan"
                && c.Timing == "WhenPlay"
                && c.Metadata.TryGetValue("buffedUnits", out var buffed)
                && buffed == "2"
        );
    }
}

