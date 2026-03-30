using Application.Features.Riftbound.Simulation.Definitions;
using Application.Features.Riftbound.Simulation.Effects;
using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Unit.Tests.Features.Riftbound.Simulation;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.RiftboundTests)]
public sealed class RiftboundSimulationEngineJCardWaveBehaviorTests
    : RiftboundSimulationEngineBehaviorTestBase
{
    [Fact]
    public void JWaveCards_ResolveToSupportedTemplates()
    {
        foreach (var card in BuildWaveCards())
        {
            var template = RiftboundEffectTemplateResolver.Resolve(card);
            Assert.True(template.Supported, $"{card.Name} resolved as unsupported.");
            Assert.NotEqual("unsupported", template.TemplateId);
        }
    }

    [Fact]
    public void JaullFish_CostIsReducedByFriendlyMightyUnits()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026033001);
        var player = session.Players[0];
        ResetPlayer(player);

        player.RunePool.Energy = 3;
        player.BaseZone.Cards.Add(BuildUnit(0, 0, "Mighty A", 5));
        player.BaseZone.Cards.Add(BuildUnit(0, 0, "Mighty B", 6));

        var jaull = BuildCardInstance(
            new RiftboundCard
            {
                Id = 100001,
                Name = "Jaull-Fish",
                Type = "Unit",
                Cost = 7,
                Power = 0,
                Might = 6,
                Color = ["Body"],
                GameplayKeywords = ["Accelerate"],
            },
            0,
            0
        );
        player.HandZone.Cards.Add(jaull);

        Assert.Contains(
            engine.GetLegalActions(session),
            x =>
                x.ActionId.Contains(jaull.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.EndsWith("-to-base", StringComparison.Ordinal)
        );
    }

    [Fact]
    public void JaxGrandmasterAtArms_Activate_AttachesDetachedEquipment_AndPaysOneEnergy()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026033002);
        var player = session.Players[0];
        ResetPlayer(player);

        var legend = BuildCardInstance(
            new RiftboundCard { Id = 100002, Name = "Jax, Grandmaster at Arms", Type = "Legend", Cost = 0, Power = 0 },
            0,
            0
        );
        var unit = BuildUnit(0, 0, "Friendly Unit", 3);
        var equipment = BuildCardInstance(
            new RiftboundCard
            {
                Id = 100003,
                Name = "Doran's Blade",
                Type = "Gear",
                Cost = 2,
                Power = 0,
                GameplayKeywords = ["Equip"],
            },
            0,
            0
        );
        player.LegendZone.Cards.Add(legend);
        player.BaseZone.Cards.Add(unit);
        player.BaseZone.Cards.Add(equipment);
        player.RunePool.Energy = 1;

        var action = engine.GetLegalActions(session)
            .First(x =>
                x.ActionType == RiftboundActionType.ActivateRune
                && x.ActionId.Contains(legend.InstanceId.ToString(), StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, action).Succeeded);

        Assert.True(legend.IsExhausted);
        Assert.Equal(0, player.RunePool.Energy);
        Assert.Equal(unit.InstanceId, equipment.AttachedToInstanceId);
    }

    [Fact]
    public void JaxUnmatched_GrantsQuickSpeedToEquipmentInPriorityWindow()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026033003);
        var player = session.Players[0];
        var opponent = session.Players[1];
        ResetPlayer(player);
        ResetPlayer(opponent);

        var opener = BuildCardInstance(
            new RiftboundCard { Id = 100004, Name = "Confront", Type = "Spell", Cost = 0, Power = 0, GameplayKeywords = ["Action"] },
            0,
            0
        );
        player.HandZone.Cards.Add(opener);

        var jax = BuildCardInstance(
            new RiftboundCard
            {
                Id = 100005,
                Name = "Jax, Unmatched",
                Type = "Unit",
                Cost = 0,
                Power = 0,
                Might = 5,
                GameplayKeywords = ["Deflect"],
            },
            1,
            1
        );
        var friendlyUnit = BuildUnit(1, 1, "Opponent Unit", 3);
        var equipment = BuildCardInstance(
            new RiftboundCard
            {
                Id = 100006,
                Name = "Doran's Blade",
                Type = "Gear",
                Cost = 0,
                Power = 0,
                GameplayKeywords = ["Equip"],
            },
            1,
            1
        );
        opponent.BaseZone.Cards.Add(jax);
        opponent.BaseZone.Cards.Add(friendlyUnit);
        opponent.HandZone.Cards.Add(equipment);

        var playOpener = engine.GetLegalActions(session)
            .First(x => x.ActionId.Contains(opener.InstanceId.ToString(), StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, playOpener).Succeeded);

        Assert.Contains(
            engine.GetLegalActions(session),
            x =>
                x.ActionType == RiftboundActionType.PlayCard
                && x.ActionId.Contains(equipment.InstanceId.ToString(), StringComparison.Ordinal)
        );
    }

    [Fact]
    public void JaxUnrelenting_WhenEquipmentAttachesToHim_PaysOneAndDrawsOne()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026033004);
        var player = session.Players[0];
        ResetPlayer(player);

        player.RunePool.Energy = 1;
        player.MainDeckZone.Cards.Add(BuildUnit(0, 0, "Drawn", 1));
        var jax = BuildCardInstance(
            new RiftboundCard
            {
                Id = 100007,
                Name = "Jax, Unrelenting",
                Type = "Unit",
                Cost = 0,
                Power = 0,
                Might = 3,
                GameplayKeywords = ["Equip", "Weaponmaster"],
            },
            0,
            0
        );
        var equipment = BuildCardInstance(
            new RiftboundCard
            {
                Id = 100008,
                Name = "Cloth Armor",
                Type = "Gear",
                Cost = 0,
                Power = 0,
                GameplayKeywords = ["Equip", "Quick-Draw", "Reaction", "Shield"],
            },
            0,
            0
        );
        player.BaseZone.Cards.Add(jax);
        player.HandZone.Cards.Add(equipment);

        var action = engine.GetLegalActions(session)
            .First(x =>
                x.ActionId.Contains(equipment.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.Contains(jax.InstanceId.ToString(), StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, action).Succeeded);

        Assert.Equal(0, player.RunePool.Energy);
        Assert.Single(player.HandZone.Cards);
        Assert.Contains(player.HandZone.Cards, x => string.Equals(x.Name, "Drawn", StringComparison.Ordinal));
    }

    [Fact]
    public void JayceManOfProgress_KillsFriendlyGear_AndLetsYouPlayGearIgnoringEnergyThisTurn()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026033005);
        var player = session.Players[0];
        ResetPlayer(player);

        player.RunePool.Energy = 0;
        var sacrificialGear = BuildCardInstance(
            new RiftboundCard { Id = 100009, Name = "Scrapheap", Type = "Gear", Cost = 1, Power = 0 },
            0,
            0
        );
        player.BaseZone.Cards.Add(sacrificialGear);
        var jayce = BuildCardInstance(
            new RiftboundCard { Id = 100010, Name = "Jayce, Man of Progress", Type = "Unit", Cost = 0, Power = 0, Might = 4 },
            0,
            0
        );
        var henge = BuildCardInstance(
            new RiftboundCard
            {
                Id = 100011,
                Name = "Ancient Henge",
                Type = "Gear",
                Cost = 2,
                Power = 0,
                GameplayKeywords = ["Reaction"],
            },
            0,
            0
        );
        player.HandZone.Cards.Add(jayce);
        player.HandZone.Cards.Add(henge);

        var playJayce = engine.GetLegalActions(session)
            .First(x =>
                x.ActionId.Contains(jayce.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.EndsWith("-to-base", StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, playJayce).Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);

        Assert.Contains(player.TrashZone.Cards, x => x.InstanceId == sacrificialGear.InstanceId);
        Assert.Contains(
            engine.GetLegalActions(session),
            x =>
                x.ActionType == RiftboundActionType.PlayCard
                && x.ActionId.Contains(henge.InstanceId.ToString(), StringComparison.Ordinal)
        );
    }

    [Fact]
    public void JeweledColossus_OnPlay_CreatesVisionPendingChoice()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026033006);
        var player = session.Players[0];
        ResetPlayer(player);

        player.MainDeckZone.Cards.Add(BuildUnit(0, 0, "Top Card", 2));
        var colossus = BuildCardInstance(
            new RiftboundCard
            {
                Id = 100012,
                Name = "Jeweled Colossus",
                Type = "Unit",
                Cost = 0,
                Power = 0,
                Might = 5,
                GameplayKeywords = ["Shield", "Vision"],
            },
            0,
            0
        );
        player.HandZone.Cards.Add(colossus);

        var play = engine.GetLegalActions(session)
            .First(x =>
                x.ActionId.Contains(colossus.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.EndsWith("-to-base", StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, play).Succeeded);

        Assert.NotNull(session.PendingChoice);
        Assert.Equal(GemcraftSeerEffect.PendingChoiceKind, session.PendingChoice!.Kind);
        Assert.Equal("Jeweled Colossus", session.PendingChoice.SourceCardName);
    }

    [Fact]
    public void JinxDemolitionist_OnPlay_DiscardsTwoCards()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026033007);
        var player = session.Players[0];
        ResetPlayer(player);

        var demolitionist = BuildCardInstance(
            new RiftboundCard
            {
                Id = 100013,
                Name = "Jinx, Demolitionist",
                Type = "Unit",
                Cost = 0,
                Power = 0,
                Might = 4,
                GameplayKeywords = ["Accelerate", "Assault"],
            },
            0,
            0
        );
        var keepCard = BuildCardInstance(new RiftboundCard { Id = 100014, Name = "Keep", Type = "Unit", Cost = 9, Might = 1 }, 0, 0);
        var discardA = BuildCardInstance(new RiftboundCard { Id = 100015, Name = "Discard A", Type = "Spell", Cost = 1, Power = 0 }, 0, 0);
        var discardB = BuildCardInstance(new RiftboundCard { Id = 100016, Name = "Discard B", Type = "Spell", Cost = 2, Power = 0 }, 0, 0);
        player.HandZone.Cards.Add(demolitionist);
        player.HandZone.Cards.Add(keepCard);
        player.HandZone.Cards.Add(discardA);
        player.HandZone.Cards.Add(discardB);

        var play = engine.GetLegalActions(session)
            .First(x =>
                x.ActionId.Contains(demolitionist.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.EndsWith("-to-base", StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, play).Succeeded);

        Assert.Single(player.HandZone.Cards);
        Assert.Equal(2, player.TrashZone.Cards.Count);
    }

    [Fact]
    public void JinxLooseCannon_AtBeginning_DrawsWhenHandHasOneOrFewerCards()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026033008);
        var player = session.Players[0];
        var opponent = session.Players[1];
        ResetPlayer(player);
        ResetPlayer(opponent);

        player.LegendZone.Cards.Add(
            BuildCardInstance(
                new RiftboundCard { Id = 100017, Name = "Jinx, Loose Cannon", Type = "Legend", Cost = 0, Power = 0 },
                0,
                0
            )
        );
        player.HandZone.Cards.Add(BuildUnit(0, 0, "Only Card", 1));
        player.MainDeckZone.Cards.Add(BuildUnit(0, 0, "Drawn", 1));

        Assert.True(engine.ApplyAction(session, "end-turn").Succeeded);
        Assert.True(engine.ApplyAction(session, "end-turn").Succeeded);

        Assert.Equal(2, player.HandZone.Cards.Count);
    }

    [Fact]
    public void JinxRebel_WhenYouDiscard_ReadiesAndGetsPlusOneMight()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026033009);
        var player = session.Players[0];
        var opponent = session.Players[1];
        ResetPlayer(player);
        ResetPlayer(opponent);

        var rebel = BuildCardInstance(
            new RiftboundCard { Id = 100018, Name = "Jinx, Rebel", Type = "Unit", Cost = 0, Power = 0, Might = 5 },
            0,
            0
        );
        rebel.IsExhausted = true;
        player.BaseZone.Cards.Add(rebel);
        var fodder = BuildCardInstance(
            new RiftboundCard { Id = 100019, Name = "Fodder", Type = "Spell", Cost = 3, Power = 0 },
            0,
            0
        );
        var getExcited = BuildCardInstance(
            new RiftboundCard
            {
                Id = 100020,
                Name = "Get Excited!",
                Type = "Spell",
                Cost = 0,
                Power = 0,
                GameplayKeywords = ["Action"],
            },
            0,
            0
        );
        player.HandZone.Cards.Add(getExcited);
        player.HandZone.Cards.Add(fodder);
        session.Battlefields[0].Units.Add(BuildUnit(1, 1, "Enemy", 3));

        var action = engine.GetLegalActions(session)
            .First(x =>
                x.ActionId.Contains(getExcited.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.Contains(fodder.InstanceId.ToString(), StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, action).Succeeded);

        Assert.False(rebel.IsExhausted);
        Assert.Equal(1, rebel.TemporaryMightModifier);
    }

    [Fact]
    public void KadregrinTheInfernal_OnPlay_DrawsForEachFriendlyMightyUnit()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026033010);
        var player = session.Players[0];
        ResetPlayer(player);

        player.BaseZone.Cards.Add(BuildUnit(0, 0, "Mighty Unit", 5));
        player.BaseZone.Cards.Add(BuildUnit(0, 0, "Small Unit", 2));
        player.MainDeckZone.Cards.Add(BuildUnit(0, 0, "Draw A", 1));
        player.MainDeckZone.Cards.Add(BuildUnit(0, 0, "Draw B", 1));

        var kadregrin = BuildCardInstance(
            new RiftboundCard
            {
                Id = 100021,
                Name = "Kadregrin the Infernal",
                Type = "Unit",
                Cost = 0,
                Power = 0,
                Might = 9,
            },
            0,
            0
        );
        player.HandZone.Cards.Add(kadregrin);

        var play = engine.GetLegalActions(session)
            .First(x =>
                x.ActionId.Contains(kadregrin.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.EndsWith("-to-base", StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, play).Succeeded);

        Assert.Equal(2, player.HandZone.Cards.Count);
    }

    private static GameSession CreateSession(RiftboundSimulationEngine engine, int seed)
    {
        return engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                simulationId: 1,
                userId: 9,
                seed: seed,
                challengerDeck: RiftboundSimulationTestData.BuildDeck(seed + 1, "Chaos"),
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
            new RiftboundCard { Id = 225, Name = "Jaull-Fish", Type = "Unit", Effect = "I cost [2] less for each of your [MIGHTY] units.", GameplayKeywords = ["Accelerate", "Mighty"] },
            new RiftboundCard { Id = 226, Name = "Jax, Grandmaster at Arms", Type = "Legend", Effect = "[1], [Tap]: Attach a detached Equipment you control to a unit you control." },
            new RiftboundCard { Id = 227, Name = "Jax, Unmatched", Type = "Unit", Effect = "Your Equipment everywhere have [Quick-Draw].", GameplayKeywords = ["Deflect", "Quick-Draw", "Reaction"] },
            new RiftboundCard { Id = 228, Name = "Jax, Unrelenting", Type = "Unit", Effect = "When you attach an Equipment to me, you may pay [1] to draw 1.", GameplayKeywords = ["Equip", "Weaponmaster"] },
            new RiftboundCard { Id = 229, Name = "Jayce, Man of Progress", Type = "Unit", Effect = "When you play me, you may kill a friendly gear. If you do, you may play a gear with Energy cost no more than [7] from hand this turn, ignoring its Energy cost." },
            new RiftboundCard { Id = 230, Name = "Jeweled Colossus", Type = "Unit", Effect = "[Vision]. [Shield].", GameplayKeywords = ["Vision", "Shield"] },
            new RiftboundCard { Id = 231, Name = "Jinx, Demolitionist", Type = "Unit", Effect = "When you play me, discard 2.", GameplayKeywords = ["Accelerate", "Assault"] },
            new RiftboundCard { Id = 232, Name = "Jinx, Loose Cannon", Type = "Legend", Effect = "At start of your Beginning Phase, draw 1 if you have 1 or fewer cards in your hand." },
            new RiftboundCard { Id = 233, Name = "Jinx, Rebel", Type = "Unit", Effect = "When you discard one or more cards, ready me and give me +1 [Might] this turn." },
            new RiftboundCard { Id = 234, Name = "Kadregrin the Infernal", Type = "Unit", Effect = "When you play me, draw 1 for each of your [MIGHTY] units.", GameplayKeywords = ["Mighty"] },
        ];
    }
}
