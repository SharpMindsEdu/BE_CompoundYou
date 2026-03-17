using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Unit.Tests.Features.Riftbound.Simulation;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.RiftboundTests)]
public sealed class RiftboundSimulationEngineFallingStarBehaviorTests : RiftboundSimulationEngineBehaviorTestBase
{
    [Fact]
    public void FallingStar_CounteredByWindWall_PreventsResolution_AndStillPaysDeflectCost()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031624,
                RiftboundSimulationTestData.BuildDeck(9997, "Fury"),
                RiftboundSimulationTestData.BuildDeck(9998, "Calm")
            )
        );

        var player = session.Players[0];
        var opponent = session.Players[1];
        player.HandZone.Cards.Clear();
        player.BaseZone.Cards.Clear();
        player.MainDeckZone.Cards.Clear();
        player.TrashZone.Cards.Clear();
        player.RunePool.Energy = 0;
        player.RunePool.PowerByDomain.Clear();
        opponent.HandZone.Cards.Clear();
        opponent.BaseZone.Cards.Clear();
        opponent.MainDeckZone.Cards.Clear();
        opponent.TrashZone.Cards.Clear();
        opponent.RunePool.Energy = 0;
        opponent.RunePool.PowerByDomain.Clear();

        player.BaseZone.Cards.Add(BuildRuneInstance(218_100, "Fury Rune", "Fury", ownerPlayer: 0));
        player.BaseZone.Cards.Add(BuildRuneInstance(218_101, "Fury Rune", "Fury", ownerPlayer: 0));
        player.BaseZone.Cards.Add(BuildRuneInstance(218_102, "Fury Rune", "Fury", ownerPlayer: 0));
        opponent.BaseZone.Cards.Add(BuildRuneInstance(218_200, "Calm Rune", "Calm", ownerPlayer: 1));
        opponent.BaseZone.Cards.Add(BuildRuneInstance(218_201, "Calm Rune", "Calm", ownerPlayer: 1));
        opponent.BaseZone.Cards.Add(BuildRuneInstance(218_202, "Calm Rune", "Calm", ownerPlayer: 1));

        var fallingStar = BuildCardInstance(
            new RiftboundCard
            {
                Id = 418_100,
                Name = "Falling Star",
                Type = "Spell",
                Cost = 2,
                Power = 2,
                Color = ["Fury"],
                GameplayKeywords = ["Action"],
                Effect = "Deal 3 to a unit. Deal 3 to a unit.",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.HandZone.Cards.Add(fallingStar);

        var windWall = BuildCardInstance(
            new RiftboundCard
            {
                Id = 418_200,
                Name = "Wind Wall",
                Type = "Spell",
                Cost = 3,
                Power = 2,
                Color = ["Calm"],
                GameplayKeywords = ["Reaction"],
                Effect = "[REACTION] Counter a spell.",
            },
            ownerPlayer: 1,
            controllerPlayer: 1
        );
        opponent.HandZone.Cards.Add(windWall);

        var irelia = BuildCardInstance(
            new RiftboundCard
            {
                Id = 418_300,
                Name = "Irelia, Fervent",
                Type = "Unit",
                Supertype = "Champion",
                Cost = 5,
                Power = 0,
                Might = 4,
                Color = ["Calm"],
                Effect = "[Deflect] (Opponents must pay [Rune] to choose me with a spell or ability.) When you choose or ready me, give me +1 [Might] this turn.",
            },
            ownerPlayer: 1,
            controllerPlayer: 1
        );
        opponent.BaseZone.Cards.Add(irelia);

        var runeDeckCountBefore = player.RuneDeckZone.Cards.Count;
        var castFallingStarAction = engine
            .GetLegalActions(session)
            .First(a =>
                a.ActionType == RiftboundActionType.PlayCard
                && a.ActionId.Contains(fallingStar.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.Contains(irelia.InstanceId.ToString(), StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, castFallingStarAction).Succeeded);

        Assert.Equal(runeDeckCountBefore + 3, player.RuneDeckZone.Cards.Count);
        Assert.Equal(0, irelia.TemporaryMightModifier);

        var playWindWallAction = engine
            .GetLegalActions(session)
            .First(a =>
                a.ActionType == RiftboundActionType.PlayCard
                && a.ActionId.Contains(windWall.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.Contains(fallingStar.InstanceId.ToString(), StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, playWindWallAction).Succeeded);

        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);

        Assert.Equal(0, irelia.MarkedDamage);
        Assert.Contains(opponent.BaseZone.Cards, x => x.InstanceId == irelia.InstanceId);
        Assert.DoesNotContain(
            session.EffectContexts,
            c => c.Source == "Falling Star" && c.Timing == "Resolve"
        );
        Assert.Contains(
            session.EffectContexts,
            c =>
                c.Source == "Wind Wall"
                && c.Timing == "Resolve"
                && c.Metadata.TryGetValue("counteredSpell", out var spell)
                && spell == "Falling Star"
        );
    }

     [Fact]
    public void FallingStar_WithDisciplineOnIrelia_IncreasesMightBy3_AndStillPaysDeflectCost()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031624,
                RiftboundSimulationTestData.BuildDeck(9997, "Fury"),
                RiftboundSimulationTestData.BuildDeck(9998, "Calm")
            )
        );

        var player = session.Players[0];
        var opponent = session.Players[1];
        player.HandZone.Cards.Clear();
        player.BaseZone.Cards.Clear();
        player.MainDeckZone.Cards.Clear();
        player.TrashZone.Cards.Clear();
        player.RunePool.Energy = 0;
        player.RunePool.PowerByDomain.Clear();
        opponent.HandZone.Cards.Clear();
        opponent.BaseZone.Cards.Clear();
        opponent.MainDeckZone.Cards.Clear();
        opponent.TrashZone.Cards.Clear();
        opponent.RunePool.Energy = 0;
        opponent.RunePool.PowerByDomain.Clear();

        player.BaseZone.Cards.Add(BuildRuneInstance(218_100, "Fury Rune", "Fury", ownerPlayer: 0));
        player.BaseZone.Cards.Add(BuildRuneInstance(218_101, "Fury Rune", "Fury", ownerPlayer: 0));
        player.BaseZone.Cards.Add(BuildRuneInstance(218_102, "Fury Rune", "Fury", ownerPlayer: 0));
        opponent.BaseZone.Cards.Add(BuildRuneInstance(218_200, "Calm Rune", "Calm", ownerPlayer: 1));
        opponent.BaseZone.Cards.Add(BuildRuneInstance(218_201, "Calm Rune", "Calm", ownerPlayer: 1));

        var fallingStar = BuildCardInstance(
            new RiftboundCard
            {
                Id = 418_100,
                Name = "Falling Star",
                Type = "Spell",
                Cost = 2,
                Power = 2,
                Color = ["Fury"],
                GameplayKeywords = ["Action"],
                Effect = "Deal 3 to a unit. Deal 3 to a unit.",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.HandZone.Cards.Add(fallingStar);

        var discipline = BuildCardInstance(
            new RiftboundCard
            {
                Id = 407_100,
                Name = "Discipline",
                Type = "Spell",
                Cost = 2,
                Power = 0,
                Color = ["Calm"],
                GameplayKeywords = ["Reaction"],
                Effect = "[REACTION] Give a unit +2 [Might] this turn. Draw 1.",
            },
            ownerPlayer: 1,
            controllerPlayer: 1
        );
        opponent.HandZone.Cards.Add(discipline);

        var irelia = BuildCardInstance(
            new RiftboundCard
            {
                Id = 418_300,
                Name = "Irelia, Fervent",
                Type = "Unit",
                Supertype = "Champion",
                Cost = 5,
                Power = 0,
                Might = 4,
                Color = ["Calm"],
                Effect = "[Deflect] (Opponents must pay [Rune] to choose me with a spell or ability.) When you choose or ready me, give me +1 [Might] this turn.",
            },
            ownerPlayer: 1,
            controllerPlayer: 1
        );
        opponent.BaseZone.Cards.Add(irelia);

        var runeDeckCountBefore = player.RuneDeckZone.Cards.Count;
        var castFallingStarAction = engine
            .GetLegalActions(session)
            .First(a =>
                a.ActionType == RiftboundActionType.PlayCard
                && a.ActionId.Contains(fallingStar.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.Contains(irelia.InstanceId.ToString(), StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, castFallingStarAction).Succeeded);

        Assert.Equal(runeDeckCountBefore + 3, player.RuneDeckZone.Cards.Count);
        Assert.Equal(0, irelia.TemporaryMightModifier);

        var playDiscipline = engine
            .GetLegalActions(session)
            .First(a =>
                a.ActionType == RiftboundActionType.PlayCard
                && a.ActionId.Contains(discipline.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.Contains(irelia.InstanceId.ToString(), StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, playDiscipline).Succeeded);

        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);

        Assert.Equal(3, irelia.TemporaryMightModifier);
        Assert.Equal(6, irelia.MarkedDamage);
        Assert.Contains(opponent.BaseZone.Cards, x => x.InstanceId == irelia.InstanceId);
        Assert.Contains(
            session.EffectContexts,
            c =>
                c.Source == "Irelia, Fervent"
                && c.Timing == "WhenChosen"
                && c.Metadata.TryGetValue("sourceCard", out var sourceCard)
                && sourceCard == "Discipline"
        );
        Assert.DoesNotContain(
            session.EffectContexts,
            c =>
                c.Source == "Irelia, Fervent"
                && c.Timing == "WhenChosen"
                && c.Metadata.TryGetValue("sourceCard", out var sourceCard)
                && sourceCard == "Falling Star"
        );
        Assert.Contains(
            session.EffectContexts,
            c =>
                c.Source == "Falling Star"
                && c.Timing == "Resolve"
                && c.Metadata.TryGetValue("target", out var target)
                && target == "Irelia, Fervent"
        );
    }

    [Fact]
    public void FallingStar_WithTwoDifferentTargets_DealsThreeToEachTarget()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031627,
                RiftboundSimulationTestData.BuildDeck(10011, "Fury"),
                RiftboundSimulationTestData.BuildDeck(10012, "Order")
            )
        );

        var player = session.Players[0];
        var opponent = session.Players[1];
        player.HandZone.Cards.Clear();
        player.BaseZone.Cards.Clear();
        player.MainDeckZone.Cards.Clear();
        player.TrashZone.Cards.Clear();
        player.RunePool.Energy = 0;
        player.RunePool.PowerByDomain.Clear();
        opponent.HandZone.Cards.Clear();
        opponent.BaseZone.Cards.Clear();
        opponent.MainDeckZone.Cards.Clear();
        opponent.TrashZone.Cards.Clear();
        opponent.RunePool.Energy = 0;
        opponent.RunePool.PowerByDomain.Clear();
        player.BaseZone.Cards.Add(BuildRuneInstance(418_410, "Fury Rune", "Fury", ownerPlayer: 0));
        player.BaseZone.Cards.Add(BuildRuneInstance(418_411, "Fury Rune", "Fury", ownerPlayer: 0));

        var fallingStar = BuildCardInstance(
            new RiftboundCard
            {
                Id = 418_400,
                Name = "Falling Star",
                Type = "Spell",
                Cost = 2,
                Power = 2,
                Color = ["Fury"],
                GameplayKeywords = ["Action"],
                Effect = "Deal 3 to a unit. Deal 3 to a unit.",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.HandZone.Cards.Add(fallingStar);

        var unitA = BuildUnit(ownerPlayer: 1, controllerPlayer: 1, name: "Target A", might: 4);
        var unitB = BuildUnit(ownerPlayer: 1, controllerPlayer: 1, name: "Target B", might: 4);
        opponent.BaseZone.Cards.Add(unitA);
        opponent.BaseZone.Cards.Add(unitB);

        var castAction = engine
            .GetLegalActions(session)
            .First(a =>
                a.ActionType == RiftboundActionType.PlayCard
                && a.ActionId.Contains(fallingStar.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.Contains("-target-units-", StringComparison.Ordinal)
                && a.ActionId.Contains(unitA.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.Contains(unitB.InstanceId.ToString(), StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, castAction).Succeeded);

        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);

        Assert.Equal(3, unitA.MarkedDamage);
        Assert.Equal(3, unitB.MarkedDamage);
        Assert.Contains(
            session.EffectContexts,
            c =>
                c.Source == "Falling Star"
                && c.Timing == "Resolve"
                && c.Metadata.TryGetValue("target", out var targets)
                && targets.Contains("Target A", StringComparison.Ordinal)
                && targets.Contains("Target B", StringComparison.Ordinal)
        );
    }
}

