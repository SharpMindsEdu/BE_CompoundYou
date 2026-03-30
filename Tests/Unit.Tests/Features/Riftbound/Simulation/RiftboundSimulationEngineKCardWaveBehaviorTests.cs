using Application.Features.Riftbound.Simulation.Definitions;
using Application.Features.Riftbound.Simulation.Effects;
using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Unit.Tests.Features.Riftbound.Simulation;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.RiftboundTests)]
public sealed class RiftboundSimulationEngineKCardWaveBehaviorTests
    : RiftboundSimulationEngineBehaviorTestBase
{
    [Fact]
    public void KWaveCards_ResolveToSupportedTemplates()
    {
        foreach (var card in BuildWaveCards())
        {
            var template = RiftboundEffectTemplateResolver.Resolve(card);
            Assert.True(template.Supported, $"{card.Name} resolved as unsupported.");
            Assert.NotEqual("unsupported", template.TemplateId);
        }
    }

    [Fact]
    public void KaiSaDaughterOfTheVoid_Activate_AddsOneRunePowerAndExhausts()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026033001);
        var player = session.Players[0];
        ResetPlayer(player);

        var legend = BuildCardInstance(
            new RiftboundCard
            {
                Id = 200001,
                Name = "Kai'Sa, Daughter of the Void",
                Type = "Legend",
                Cost = 0,
                Power = 0,
                GameplayKeywords = ["Reaction"],
            },
            0,
            0
        );
        player.LegendZone.Cards.Add(legend);

        var action = engine.GetLegalActions(session)
            .First(x => x.ActionId.Contains(legend.InstanceId.ToString(), StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, action).Succeeded);

        Assert.True(legend.IsExhausted);
        Assert.Equal(1, ReadPower(player, "__unknown__"));
    }

    [Fact]
    public void KaiSaEvolutionary_OnConquer_PlaysSpellFromTrashIgnoringEnergy_ThenRecyclesIt()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026033002);
        var player = session.Players[0];
        var opponent = session.Players[1];
        ResetPlayer(player);
        ResetPlayer(opponent);

        player.Score = 4;
        session.Battlefields[0].ControlledByPlayerIndex = opponent.PlayerIndex;
        var kaiSa = BuildCardInstance(
            new RiftboundCard
            {
                Id = 200002,
                Name = "Kai'Sa, Evolutionary",
                Type = "Unit",
                Cost = 0,
                Power = 0,
                Might = 6,
                GameplayKeywords = ["Ganking"],
            },
            0,
            0
        );
        player.BaseZone.Cards.Add(kaiSa);
        var stackedDeck = BuildCardInstance(
            new RiftboundCard
            {
                Id = 200003,
                Name = "Stacked Deck",
                Type = "Spell",
                Cost = 1,
                Power = 0,
                GameplayKeywords = ["Action"],
            },
            0,
            0
        );
        player.TrashZone.Cards.Add(stackedDeck);

        var moveAction = engine.GetLegalActions(session)
            .First(x =>
                x.ActionId.Contains(kaiSa.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.EndsWith("-to-bf-0", StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, moveAction).Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);

        Assert.DoesNotContain(player.TrashZone.Cards, x => x.InstanceId == stackedDeck.InstanceId);
        Assert.Contains(player.MainDeckZone.Cards, x => x.InstanceId == stackedDeck.InstanceId);
    }

    [Fact]
    public void KarmaChanneler_WhenCardsAreRecycled_BuffsFriendlyUnit()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026033003);
        var player = session.Players[0];
        ResetPlayer(player);

        var karma = BuildCardInstance(
            new RiftboundCard
            {
                Id = 200004,
                Name = "Karma, Channeler",
                Type = "Unit",
                Cost = 0,
                Power = 0,
                Might = 6,
                GameplayKeywords = ["Vision"],
            },
            0,
            0
        );
        var buffTarget = BuildUnit(0, 0, "Friendly Target", 3);
        player.BaseZone.Cards.Add(karma);
        player.BaseZone.Cards.Add(buffTarget);
        player.MainDeckZone.Cards.Add(BuildUnit(0, 0, "Top A", 1));
        player.MainDeckZone.Cards.Add(BuildUnit(0, 0, "Top B", 1));
        player.MainDeckZone.Cards.Add(BuildUnit(0, 0, "Top C", 1));

        var stackedDeck = BuildCardInstance(
            new RiftboundCard
            {
                Id = 200005,
                Name = "Stacked Deck",
                Type = "Spell",
                Cost = 0,
                Power = 0,
                GameplayKeywords = ["Action"],
            },
            0,
            0
        );
        player.HandZone.Cards.Add(stackedDeck);

        var playAction = engine.GetLegalActions(session)
            .First(x => x.ActionId.Contains(stackedDeck.InstanceId.ToString(), StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, playAction).Succeeded);

        var chooseAction = engine.GetLegalActions(session).First().ActionId;
        Assert.True(engine.ApplyAction(session, chooseAction).Succeeded);

        Assert.Equal(1, buffTarget.PermanentMightModifier + karma.PermanentMightModifier);
    }

    [Fact]
    public void KarthusEternal_DoublesFriendlyDeathknellTriggers()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026033004);
        var player = session.Players[0];
        ResetPlayer(player);

        var karthus = BuildCardInstance(
            new RiftboundCard
            {
                Id = 200006,
                Name = "Karthus, Eternal",
                Type = "Unit",
                Cost = 0,
                Power = 0,
                Might = 3,
                GameplayKeywords = ["Deathknell"],
            },
            0,
            0
        );
        var forerunner = BuildCardInstance(
            new RiftboundCard
            {
                Id = 200007,
                Name = "Ferrous Forerunner",
                Type = "Unit",
                Cost = 0,
                Power = 0,
                Might = 1,
                GameplayKeywords = ["Deathknell"],
            },
            0,
            0
        );
        forerunner.MarkedDamage = 1;
        var mover = BuildUnit(0, 0, "Mover", 2);
        player.BaseZone.Cards.Add(karthus);
        player.BaseZone.Cards.Add(forerunner);
        player.BaseZone.Cards.Add(mover);

        var moveAction = engine.GetLegalActions(session)
            .First(x =>
                x.ActionId.Contains(mover.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.Contains("-to-bf-", StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, moveAction).Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);

        Assert.Equal(4, player.BaseZone.Cards.Count(x => string.Equals(x.Name, "Mech Token", StringComparison.Ordinal)));
    }

    [Fact]
    public void KatoTheArm_WhenMovingToBattlefield_GrantsKeywordsAndTemporaryMightToOtherFriendlyUnit()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026033005);
        var player = session.Players[0];
        ResetPlayer(player);

        session.Battlefields[0].ControlledByPlayerIndex = player.PlayerIndex;
        var kato = BuildCardInstance(
            new RiftboundCard
            {
                Id = 200008,
                Name = "Kato the Arm",
                Type = "Unit",
                Cost = 0,
                Power = 0,
                Might = 3,
                GameplayKeywords = ["Deflect"],
            },
            0,
            0
        );
        var target = BuildUnit(0, 0, "Friendly Battlefield Unit", 2);
        player.BaseZone.Cards.Add(kato);
        session.Battlefields[0].Units.Add(target);

        var moveAction = engine.GetLegalActions(session)
            .First(x =>
                x.ActionId.Contains(kato.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.EndsWith("-to-bf-0", StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, moveAction).Succeeded);

        Assert.Equal(3, target.TemporaryMightModifier);
        Assert.True(target.EffectData.ContainsKey("temporaryKeyword.Deflect"));
    }

    [Fact]
    public void KaynUnleashed_AfterTwoMoves_DoesNotTakeDamageThisTurn()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026033006);
        var player = session.Players[0];
        var opponent = session.Players[1];
        ResetPlayer(player);
        ResetPlayer(opponent);

        session.Battlefields[0].ControlledByPlayerIndex = player.PlayerIndex;
        session.Battlefields[1].ControlledByPlayerIndex = player.PlayerIndex;
        var kayn = BuildCardInstance(
            new RiftboundCard
            {
                Id = 200009,
                Name = "Kayn, Unleashed",
                Type = "Unit",
                Cost = 0,
                Power = 0,
                Might = 6,
                GameplayKeywords = ["Ganking"],
            },
            0,
            0
        );
        player.BaseZone.Cards.Add(kayn);

        var moveToBf0 = engine.GetLegalActions(session)
            .First(x =>
                x.ActionId.Contains(kayn.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.EndsWith("-to-bf-0", StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, moveToBf0).Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);

        kayn.IsExhausted = false;

        var moveToBase = engine.GetLegalActions(session)
            .First(x =>
                x.ActionId.Contains(kayn.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.EndsWith("-to-base", StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, moveToBase).Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);

        kayn.MarkedDamage = 3;
        var rune = BuildRuneInstance(200010, "Body Rune", "Body", ownerPlayer: 0);
        player.BaseZone.Cards.Add(rune);
        var activateRune = engine.GetLegalActions(session)
            .First(x => x.ActionId.Contains(rune.InstanceId.ToString(), StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, activateRune).Succeeded);

        Assert.Equal(0, kayn.MarkedDamage);
    }

    [Fact]
    public void KingsEdict_OtherPlayerChoosesUnit_AndChosenUnitDies()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026033007);
        var player = session.Players[0];
        var opponent = session.Players[1];
        ResetPlayer(player);
        ResetPlayer(opponent);

        var kingsEdict = BuildCardInstance(
            new RiftboundCard
            {
                Id = 200011,
                Name = "King's Edict",
                Type = "Spell",
                Cost = 0,
                Power = 0,
            },
            0,
            0
        );
        player.HandZone.Cards.Add(kingsEdict);
        var targetA = BuildUnit(1, 1, "Target A", 3);
        var targetB = BuildUnit(1, 1, "Target B", 3);
        opponent.BaseZone.Cards.Add(targetA);
        opponent.BaseZone.Cards.Add(targetB);

        var playAction = engine.GetLegalActions(session)
            .First(x => x.ActionId.Contains(kingsEdict.InstanceId.ToString(), StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, playAction).Succeeded);

        Assert.NotNull(session.PendingChoice);
        Assert.Equal(opponent.PlayerIndex, session.PendingChoice!.PlayerIndex);
        var chooseB = engine.GetLegalActions(session)
            .First(x => x.ActionId.Contains(targetB.InstanceId.ToString(), StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, chooseB).Succeeded);

        Assert.DoesNotContain(opponent.BaseZone.Cards, x => x.InstanceId == targetB.InstanceId);
        Assert.Contains(opponent.TrashZone.Cards, x => x.InstanceId == targetB.InstanceId);
        Assert.Contains(opponent.BaseZone.Cards, x => x.InstanceId == targetA.InstanceId);
    }

    [Fact]
    public void KinkouMonk_AllowsChoosingUpToTwoOtherFriendlyUnitsToBuff()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026033008);
        var player = session.Players[0];
        ResetPlayer(player);

        var targetA = BuildUnit(0, 0, "Target A", 2);
        var targetB = BuildUnit(0, 0, "Target B", 2);
        var targetC = BuildUnit(0, 0, "Target C", 2);
        player.BaseZone.Cards.Add(targetA);
        player.BaseZone.Cards.Add(targetB);
        player.BaseZone.Cards.Add(targetC);
        var monk = BuildCardInstance(
            new RiftboundCard
            {
                Id = 200012,
                Name = "Kinkou Monk",
                Type = "Unit",
                Cost = 0,
                Power = 0,
                Might = 4,
            },
            0,
            0
        );
        player.HandZone.Cards.Add(monk);

        var playAction = engine.GetLegalActions(session)
            .First(x => x.ActionId.Contains(monk.InstanceId.ToString(), StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, playAction).Succeeded);
        Assert.NotNull(session.PendingChoice);

        var chooseA = engine.GetLegalActions(session)
            .First(x => x.ActionId.Contains(targetA.InstanceId.ToString(), StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, chooseA).Succeeded);
        Assert.NotNull(session.PendingChoice);

        var done = engine.GetLegalActions(session)
            .First(x => x.ActionId.EndsWith("choose-kinkou-monk-done", StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, done).Succeeded);

        Assert.Equal(1, targetA.PermanentMightModifier);
        Assert.Equal(0, targetB.PermanentMightModifier);
        Assert.Equal(0, targetC.PermanentMightModifier);
    }

    [Fact]
    public void KogMawCaustic_Deathknell_DealsFourToAllUnitsAtItsBattlefield()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026033009);
        var player = session.Players[0];
        var opponent = session.Players[1];
        ResetPlayer(player);
        ResetPlayer(opponent);

        var kogMaw = BuildCardInstance(
            new RiftboundCard
            {
                Id = 200013,
                Name = "Kog'Maw, Caustic",
                Type = "Unit",
                Cost = 0,
                Power = 0,
                Might = 1,
                GameplayKeywords = ["Deathknell"],
            },
            0,
            0
        );
        var friendly = BuildUnit(0, 0, "Friendly", 10);
        var enemy = BuildUnit(1, 1, "Enemy", 10);
        session.Battlefields[0].Units.Add(kogMaw);
        session.Battlefields[0].Units.Add(friendly);
        session.Battlefields[0].Units.Add(enemy);
        kogMaw.MarkedDamage = 1;
        var mover = BuildUnit(0, 0, "Mover", 2);
        player.BaseZone.Cards.Add(mover);

        var moveAction = engine.GetLegalActions(session)
            .First(x => x.ActionId.Contains(mover.InstanceId.ToString(), StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, moveAction).Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);

        Assert.Equal(4, friendly.MarkedDamage);
        Assert.Equal(4, enemy.MarkedDamage);
    }

    [Fact]
    public void KrakenHunter_WhenSpendingBuff_ReducesBodyCostAndSpendsBuff()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026033010);
        var player = session.Players[0];
        ResetPlayer(player);

        var buffSource = BuildUnit(0, 0, "Buff Source", 3);
        buffSource.PermanentMightModifier = 1;
        player.BaseZone.Cards.Add(buffSource);
        player.RunePool.Energy = 3;
        player.RunePool.PowerByDomain["Body"] = 2;

        var kraken = BuildCardInstance(
            new RiftboundCard
            {
                Id = 200014,
                Name = "Kraken Hunter",
                Type = "Unit",
                Cost = 3,
                Power = 2,
                Might = 5,
                Color = ["Body"],
                GameplayKeywords = ["Accelerate", "Assault"],
            },
            0,
            0
        );
        player.HandZone.Cards.Add(kraken);

        var playWithBuffSpend = engine.GetLegalActions(session)
            .First(x =>
                x.ActionId.Contains(kraken.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.Contains(KrakenHunterEffect.SpendBuffMarker, StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, playWithBuffSpend).Succeeded);

        Assert.Equal(0, player.RunePool.Energy);
        Assert.Equal(1, ReadPower(player, "Body"));
        Assert.Equal(0, buffSource.PermanentMightModifier);
    }

    private static GameSession CreateSession(RiftboundSimulationEngine engine, int seed)
    {
        return engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                simulationId: 1,
                userId: 11,
                seed: seed,
                challengerDeck: RiftboundSimulationTestData.BuildDeck(seed + 1, "Mind"),
                opponentDeck: RiftboundSimulationTestData.BuildDeck(seed + 2, "Order")
            )
        );
    }

    private static void ResetPlayer(PlayerState player)
    {
        player.HandZone.Cards.Clear();
        player.BaseZone.Cards.Clear();
        player.MainDeckZone.Cards.Clear();
        player.RuneDeckZone.Cards.Clear();
        player.TrashZone.Cards.Clear();
        player.LegendZone.Cards.Clear();
        player.ChampionZone.Cards.Clear();
        player.RunePool.Energy = 0;
        player.RunePool.PowerByDomain.Clear();
    }

    private static IReadOnlyCollection<RiftboundCard> BuildWaveCards()
    {
        return
        [
            new RiftboundCard { Id = 235, Name = "Kai'Sa, Daughter of the Void", Type = "Legend", Effect = "[Tap]: [REACTION] - [ADD] [Rune].", GameplayKeywords = ["Reaction"] },
            new RiftboundCard { Id = 236, Name = "Kai'Sa, Evolutionary", Type = "Unit", Effect = "When I conquer, you may play a spell from your trash with Energy cost less than your points without paying its Energy cost. Then recycle it.", GameplayKeywords = ["Ganking"] },
            new RiftboundCard { Id = 238, Name = "Karma, Channeler", Type = "Unit", Effect = "When you recycle one or more cards to your Main Deck, buff a friendly unit.", GameplayKeywords = ["Vision"] },
            new RiftboundCard { Id = 239, Name = "Karthus, Eternal", Type = "Unit", Effect = "Your [Deathknell] trigger an additional time." },
            new RiftboundCard { Id = 240, Name = "Kato the Arm", Type = "Unit", Effect = "When I move to a battlefield, give another friendly unit my keywords and +[Might] equal to my Might this turn.", GameplayKeywords = ["Deflect"] },
            new RiftboundCard { Id = 241, Name = "Kayn, Unleashed", Type = "Unit", Effect = "If I have moved twice this turn, I don't take damage.", GameplayKeywords = ["Ganking"] },
            new RiftboundCard { Id = 242, Name = "King's Edict", Type = "Spell", Effect = "Starting with the next player, each other player chooses a unit you don't control that hasn't been chosen for this spell. Kill those units." },
            new RiftboundCard { Id = 243, Name = "Kinkou Monk", Type = "Unit", Effect = "When you play me, buff up to two other friendly units." },
            new RiftboundCard { Id = 244, Name = "Kog'Maw, Caustic", Type = "Unit", Effect = "[Deathknell] - Deal 4 to all units at my battlefield.", GameplayKeywords = ["Deathknell"] },
            new RiftboundCard { Id = 245, Name = "Kraken Hunter", Type = "Unit", Effect = "As you play me, you may spend any number of buffs as an additional cost. Reduce my cost by [Body] for each buff you spend.", GameplayKeywords = ["Accelerate", "Assault"] },
        ];
    }
}
