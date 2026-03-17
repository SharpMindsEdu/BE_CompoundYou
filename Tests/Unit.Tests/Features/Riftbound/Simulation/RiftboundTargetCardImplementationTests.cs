using Application.Features.Riftbound.Simulation.Definitions;
using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Unit.Tests.Features.Riftbound.Simulation;

[Trait("category", ServiceTestCategories.UnitTests)]
public class RiftboundTargetCardImplementationTests
{
    [Fact]
    public void TargetListCards_ResolveToSupportedTemplates()
    {
        foreach (var card in BuildTargetCards())
        {
            var template = RiftboundEffectTemplateResolver.Resolve(card);
            Assert.True(template.Supported, $"{card.Name} resolved as unsupported.");
            Assert.NotEqual("unsupported", template.TemplateId);
        }
    }

    [Fact]
    public void Bushwhack_CreatesGoldToken_AndFriendlyUnitsEnterReadyThisTurn()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, seed: 6201);
        var player = session.Players[0];

        ResetPlayer(player);
        var bushwhack = BuildCardInstance(
            new RiftboundCard { Id = 6201_100, Name = "Bushwhack", Type = "Spell", Cost = 0, Power = 0, GameplayKeywords = ["Hidden"] },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        var unit = BuildCardInstance(
            new RiftboundCard { Id = 6201_101, Name = "Soldier", Type = "Unit", Cost = 0, Might = 2 },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.HandZone.Cards.Add(bushwhack);
        player.HandZone.Cards.Add(unit);

        var castAction = engine.GetLegalActions(session)
            .First(x => x.ActionId.Contains(bushwhack.InstanceId.ToString(), StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, castAction).Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);

        var playUnitAction = engine.GetLegalActions(session)
            .First(x =>
                x.ActionType == RiftboundActionType.PlayCard
                && x.ActionId.Contains(unit.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.EndsWith("-to-base", StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, playUnitAction).Succeeded);

        var playedUnit = player.BaseZone.Cards.Single(x => x.InstanceId == unit.InstanceId);
        Assert.False(playedUnit.IsExhausted);
        Assert.Contains(
            player.BaseZone.Cards,
            x =>
                string.Equals(x.Name, "Gold Token", StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.Type, "Gear", StringComparison.OrdinalIgnoreCase)
                && x.IsExhausted
        );
    }

    [Fact]
    public void FightOrFlight_MovesBattlefieldUnitToOwnersBase()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, seed: 6202);
        var player = session.Players[0];
        var opponent = session.Players[1];
        ResetPlayer(player);
        ResetPlayer(opponent);

        var spell = BuildCardInstance(
            new RiftboundCard { Id = 6202_100, Name = "Fight or Flight", Type = "Spell", Cost = 0, Power = 0, GameplayKeywords = ["Action", "Hidden"] },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.HandZone.Cards.Add(spell);

        var target = BuildCardInstance(
            new RiftboundCard { Id = 6202_101, Name = "Enemy Unit", Type = "Unit", Cost = 0, Might = 3 },
            ownerPlayer: 1,
            controllerPlayer: 1
        );
        session.Battlefields[0].Units.Add(target);

        var action = engine.GetLegalActions(session)
            .First(x =>
                x.ActionId.Contains(spell.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.Contains(target.InstanceId.ToString(), StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, action).Succeeded);

        Assert.DoesNotContain(session.Battlefields[0].Units, x => x.InstanceId == target.InstanceId);
        Assert.Contains(opponent.BaseZone.Cards, x => x.InstanceId == target.InstanceId);
    }

    [Fact]
    public void Rebuke_ReturnsBattlefieldUnitToOwnerHand()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, seed: 6203);
        var player = session.Players[0];
        var opponent = session.Players[1];
        ResetPlayer(player);
        ResetPlayer(opponent);

        var spell = BuildCardInstance(
            new RiftboundCard { Id = 6203_100, Name = "Rebuke", Type = "Spell", Cost = 0, Power = 0, GameplayKeywords = ["Action"] },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.HandZone.Cards.Add(spell);
        var target = BuildCardInstance(
            new RiftboundCard { Id = 6203_101, Name = "Enemy Unit", Type = "Unit", Cost = 0, Might = 4 },
            ownerPlayer: 1,
            controllerPlayer: 1
        );
        session.Battlefields[1].Units.Add(target);

        var action = engine.GetLegalActions(session)
            .First(x =>
                x.ActionId.Contains(spell.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.Contains(target.InstanceId.ToString(), StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, action).Succeeded);

        Assert.DoesNotContain(session.Battlefields[1].Units, x => x.InstanceId == target.InstanceId);
        Assert.Contains(opponent.HandZone.Cards, x => x.InstanceId == target.InstanceId);
    }

    [Fact]
    public void RideTheWind_MovesFriendlyUnitAndReadiesIt()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, seed: 6204);
        var player = session.Players[0];
        ResetPlayer(player);

        var spell = BuildCardInstance(
            new RiftboundCard { Id = 6204_100, Name = "Ride the Wind", Type = "Spell", Cost = 0, Power = 0, GameplayKeywords = ["Action"] },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.HandZone.Cards.Add(spell);

        var unit = BuildCardInstance(
            new RiftboundCard { Id = 6204_101, Name = "Scout", Type = "Unit", Cost = 0, Might = 2 },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        unit.IsExhausted = true;
        player.BaseZone.Cards.Add(unit);

        var action = engine.GetLegalActions(session)
            .First(x =>
                x.ActionId.Contains(spell.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.Contains(unit.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.EndsWith("-to-bf-0", StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, action).Succeeded);

        Assert.Contains(session.Battlefields[0].Units, x => x.InstanceId == unit.InstanceId);
        Assert.False(session.Battlefields[0].Units.Single(x => x.InstanceId == unit.InstanceId).IsExhausted);
    }

    [Fact]
    public void Switcheroo_SwapsMightOfTwoUnitsAtSameBattlefield()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, seed: 6205);
        var player = session.Players[0];
        ResetPlayer(player);

        var spell = BuildCardInstance(
            new RiftboundCard { Id = 6205_100, Name = "Switcheroo", Type = "Spell", Cost = 0, Power = 0, GameplayKeywords = ["Action", "Hidden"] },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.HandZone.Cards.Add(spell);

        var left = BuildCardInstance(
            new RiftboundCard { Id = 6205_101, Name = "Left", Type = "Unit", Cost = 0, Might = 2 },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        var right = BuildCardInstance(
            new RiftboundCard { Id = 6205_102, Name = "Right", Type = "Unit", Cost = 0, Might = 5 },
            ownerPlayer: 1,
            controllerPlayer: 1
        );
        session.Battlefields[0].Units.Add(left);
        session.Battlefields[0].Units.Add(right);

        var action = engine.GetLegalActions(session)
            .First(x =>
                x.ActionId.Contains(spell.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.Contains(left.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.Contains(right.InstanceId.ToString(), StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, action).Succeeded);

        Assert.Equal(5, left.Might.GetValueOrDefault() + left.PermanentMightModifier + left.TemporaryMightModifier);
        Assert.Equal(2, right.Might.GetValueOrDefault() + right.PermanentMightModifier + right.TemporaryMightModifier);
    }

    [Fact]
    public void ThermoBeam_KillsAllGear_AndScrapheapDrawsOne()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, seed: 6206);
        var player = session.Players[0];
        var opponent = session.Players[1];
        ResetPlayer(player);
        ResetPlayer(opponent);

        player.MainDeckZone.Cards.Add(
            BuildCardInstance(
                new RiftboundCard { Id = 6206_001, Name = "Draw Fodder", Type = "Unit", Cost = 0, Might = 1 },
                ownerPlayer: 0,
                controllerPlayer: 0
            )
        );

        var thermo = BuildCardInstance(
            new RiftboundCard { Id = 6206_100, Name = "Thermo Beam", Type = "Spell", Cost = 0, Power = 0, GameplayKeywords = ["Action"] },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.HandZone.Cards.Add(thermo);

        var scrapheap = BuildCardInstance(
            new RiftboundCard { Id = 6206_101, Name = "Scrapheap", Type = "Gear", Cost = 0, Power = 0 },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        var enemyGear = BuildCardInstance(
            new RiftboundCard { Id = 6206_102, Name = "Enemy Gear", Type = "Gear", Cost = 0, Power = 0 },
            ownerPlayer: 1,
            controllerPlayer: 1
        );
        player.BaseZone.Cards.Add(scrapheap);
        opponent.BaseZone.Cards.Add(enemyGear);
        var handBefore = player.HandZone.Cards.Count;

        var action = engine.GetLegalActions(session)
            .First(x => x.ActionId.Contains(thermo.InstanceId.ToString(), StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, action).Succeeded);

        Assert.DoesNotContain(player.BaseZone.Cards, x => string.Equals(x.Type, "Gear", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(opponent.BaseZone.Cards, x => string.Equals(x.Type, "Gear", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(player.TrashZone.Cards, x => x.InstanceId == scrapheap.InstanceId);
        Assert.Contains(opponent.TrashZone.Cards, x => x.InstanceId == enemyGear.InstanceId);
        Assert.Equal(handBefore, player.HandZone.Cards.Count);
    }

    [Fact]
    public void HardBargain_CountersSpell_WhenControllerCannotPayTwoEnergy()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, seed: 6207);
        var player = session.Players[0];
        var opponent = session.Players[1];
        ResetPlayer(player);
        ResetPlayer(opponent);

        var attackSpell = BuildCardInstance(
            new RiftboundCard { Id = 6207_100, Name = "Simple Spell", Type = "Spell", Cost = 0, Power = 0 },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        var hardBargain = BuildCardInstance(
            new RiftboundCard { Id = 6207_101, Name = "Hard Bargain", Type = "Spell", Cost = 0, Power = 0, GameplayKeywords = ["Reaction", "Repeat"] },
            ownerPlayer: 1,
            controllerPlayer: 1
        );
        player.HandZone.Cards.Add(attackSpell);
        opponent.HandZone.Cards.Add(hardBargain);

        var spellAction = engine.GetLegalActions(session)
            .First(x => x.ActionId.Contains(attackSpell.InstanceId.ToString(), StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, spellAction).Succeeded);

        var hardBargainAction = engine.GetLegalActions(session)
            .First(x => x.ActionId.Contains(hardBargain.InstanceId.ToString(), StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, hardBargainAction).Succeeded);

        var spellChainItem = session.Chain.First(x => x.CardInstanceId == attackSpell.InstanceId);
        Assert.True(spellChainItem.IsCountered);
    }

    [Fact]
    public void BatteringRam_CostIsReducedByCardsPlayedThisTurn()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, seed: 6208);
        var player = session.Players[0];
        ResetPlayer(player);
        player.BaseZone.Cards.Add(BuildRuneInstance(6208_001, "Fury", ownerPlayer: 0));
        player.BaseZone.Cards.Add(BuildRuneInstance(6208_002, "Fury", ownerPlayer: 0));
        player.BaseZone.Cards.Add(BuildRuneInstance(6208_003, "Fury", ownerPlayer: 0));
        player.BaseZone.Cards.Add(BuildRuneInstance(6208_004, "Fury", ownerPlayer: 0));

        var cheapSpell = BuildCardInstance(
            new RiftboundCard { Id = 6208_100, Name = "Cheap", Type = "Spell", Cost = 0, Power = 0 },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        var batteringRam = BuildCardInstance(
            new RiftboundCard { Id = 6208_101, Name = "Battering Ram", Type = "Unit", Cost = 5, Power = 0, Might = 5 },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.HandZone.Cards.Add(cheapSpell);
        player.HandZone.Cards.Add(batteringRam);

        var cheapAction = engine.GetLegalActions(session)
            .First(x => x.ActionId.Contains(cheapSpell.InstanceId.ToString(), StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, cheapAction).Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);

        Assert.Contains(
            engine.GetLegalActions(session),
            x =>
                x.ActionId.Contains(batteringRam.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.EndsWith("-to-base", StringComparison.Ordinal)
        );
    }

    private static GameSession CreateSession(RiftboundSimulationEngine engine, int seed)
    {
        return engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                simulationId: 1,
                userId: 9,
                seed: seed,
                challengerDeck: RiftboundSimulationTestData.BuildDeck(1, "Chaos"),
                opponentDeck: RiftboundSimulationTestData.BuildDeck(2, "Order")
            )
        );
    }

    private static void ResetPlayer(PlayerState player)
    {
        player.HandZone.Cards.Clear();
        player.BaseZone.Cards.Clear();
        player.MainDeckZone.Cards.Clear();
        player.TrashZone.Cards.Clear();
        player.ChampionZone.Cards.Clear();
        player.LegendZone.Cards.Clear();
        player.RuneDeckZone.Cards.Clear();
        player.RunePool.Energy = 0;
        player.RunePool.PowerByDomain.Clear();
    }

    private static CardInstance BuildRuneInstance(long cardId, string domain, int ownerPlayer)
    {
        return BuildCardInstance(
            new RiftboundCard
            {
                Id = cardId,
                Name = $"{domain} Rune",
                Type = "Rune",
                Color = [domain],
            },
            ownerPlayer,
            ownerPlayer
        );
    }

    private static CardInstance BuildCardInstance(
        RiftboundCard card,
        int ownerPlayer,
        int controllerPlayer
    )
    {
        var template = RiftboundEffectTemplateResolver.Resolve(card);
        var keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (card.GameplayKeywords is not null)
        {
            foreach (var keyword in card.GameplayKeywords.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                keywords.Add(keyword.Trim());
            }
        }

        if (card.Tags is not null)
        {
            foreach (var tag in card.Tags.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                keywords.Add(tag.Trim());
            }
        }

        foreach (var keyword in template.Keywords)
        {
            keywords.Add(keyword);
        }

        return new CardInstance
        {
            InstanceId = Guid.NewGuid(),
            CardId = card.Id,
            Name = card.Name,
            Type = card.Type ?? "Card",
            OwnerPlayerIndex = ownerPlayer,
            ControllerPlayerIndex = controllerPlayer,
            Cost = card.Cost,
            Power = card.Power,
            ColorDomains = card.Color?.ToList() ?? [],
            Might = card.Might,
            Keywords = keywords.ToList(),
            EffectTemplateId = template.TemplateId,
            EffectData = template.Data.ToDictionary(
                x => x.Key,
                x => x.Value,
                StringComparer.OrdinalIgnoreCase
            ),
        };
    }

    private static IReadOnlyCollection<RiftboundCard> BuildTargetCards()
    {
        return
        [
            new RiftboundCard { Id = 1, Name = "Draven, Glorious Executioner", Type = "Legend", Effect = "When you win a combat, draw 1." },
            new RiftboundCard { Id = 2, Name = "Draven, Vanquisher", Type = "Unit", Effect = "When I win a combat, play a Gold gear token exhausted." },
            new RiftboundCard { Id = 3, Name = "Brynhir Thundersong", Type = "Unit", Effect = "When you play me, opponents can't play cards this turn." },
            new RiftboundCard { Id = 4, Name = "Darius, Trifarian", Type = "Unit", Effect = "When you play your second card in a turn, give me +2 [Might] this turn and ready me." },
            new RiftboundCard { Id = 5, Name = "Falling Star", Type = "Spell", Effect = "Deal 3 to a unit. Deal 3 to a unit." },
            new RiftboundCard { Id = 6, Name = "Ferrous Forerunner", Type = "Unit", Effect = "[DEATHKNELL] — Play two 3 [Might] Mech unit tokens to your base." },
            new RiftboundCard { Id = 7, Name = "Fight or Flight", Type = "Spell", Effect = "Move a unit from a battlefield to its base.", GameplayKeywords = ["Action", "Hidden"] },
            new RiftboundCard { Id = 8, Name = "Hard Bargain", Type = "Spell", Effect = "Counter a spell unless its controller pays [2].", GameplayKeywords = ["Reaction", "Repeat"] },
            new RiftboundCard { Id = 9, Name = "Kai'Sa, Survivor", Type = "Unit", Effect = "When I conquer, draw 1.", GameplayKeywords = ["Accelerate"] },
            new RiftboundCard { Id = 10, Name = "Noxus Hopeful", Type = "Unit", Effect = "I cost [2] less if you've played another card this turn.", GameplayKeywords = ["Legion"] },
            new RiftboundCard { Id = 11, Name = "Overzealous Fan", Type = "Unit", Effect = "When I defend, you may kill me to move an attacking unit to its base." },
            new RiftboundCard { Id = 12, Name = "Rebuke", Type = "Spell", Effect = "Return a unit at a battlefield to its owner's hand.", GameplayKeywords = ["Action"] },
            new RiftboundCard { Id = 13, Name = "Ride the Wind", Type = "Spell", Effect = "Move a friendly unit and ready it.", GameplayKeywords = ["Action"] },
            new RiftboundCard { Id = 14, Name = "Spinning Axe", Type = "Gear", Effect = "[Equip] [Rune] +3 [Might]", GameplayKeywords = ["Equip", "Quick-Draw", "Reaction"] },
            new RiftboundCard { Id = 15, Name = "Stacked Deck", Type = "Spell", Effect = "Look at the top 3 cards of your Main Deck. Put 1 into your hand and recycle the rest.", GameplayKeywords = ["Action"] },
            new RiftboundCard { Id = 16, Name = "Tideturner", Type = "Unit", Effect = "When you play me, you may choose a unit you control at another location. Move me to its location and it to my original location.", GameplayKeywords = ["Hidden"] },
            new RiftboundCard { Id = 17, Name = "Treasure Hunter", Type = "Unit", Effect = "When I move, play a Gold gear token exhausted." },
            new RiftboundCard { Id = 18, Name = "Scrapheap", Type = "Gear", Effect = "When this is played, discarded, or killed, draw 1." },
            new RiftboundCard { Id = 19, Name = "Traveling Merchant", Type = "Unit", Effect = "When I move, discard 1, then draw 1." },
            new RiftboundCard { Id = 20, Name = "Rhasa the Sunderer", Type = "Unit", Effect = "I cost [1] less for each card in your trash." },
            new RiftboundCard { Id = 21, Name = "Seal of Discord", Type = "Gear", Effect = "[Tap]: [REACTION] - [ADD] [Chaos].", GameplayKeywords = ["Reaction"] },
            new RiftboundCard { Id = 22, Name = "Battering Ram", Type = "Unit", Effect = "I cost [1] less for each card you've played this turn, to a minimum of [1]." },
            new RiftboundCard { Id = 23, Name = "Called Shot", Type = "Spell", Effect = "Look at the top 2 cards of your Main Deck. Draw one and recycle the other.", GameplayKeywords = ["Action", "Repeat"] },
            new RiftboundCard { Id = 24, Name = "Bushwhack", Type = "Spell", Effect = "Friendly units enter ready this turn. Play a Gold gear token exhausted.", GameplayKeywords = ["Hidden"] },
            new RiftboundCard { Id = 25, Name = "Ezreal, Prodigy", Type = "Unit", Effect = "When you play me, discard 1, then draw 2." },
            new RiftboundCard { Id = 26, Name = "Fizz, Trickster", Type = "Unit", Effect = "When you play me, you may play a spell from your trash with Energy cost no more than [3], ignoring its Energy cost. Recycle that spell after you play it." },
            new RiftboundCard { Id = 27, Name = "Mindsplitter", Type = "Unit", Effect = "When you play me, choose an opponent. They reveal their hand. Choose a card from it, and they discard that card." },
            new RiftboundCard { Id = 28, Name = "Thermo Beam", Type = "Spell", Effect = "Kill all gear.", GameplayKeywords = ["Action"] },
            new RiftboundCard { Id = 29, Name = "Switcheroo", Type = "Spell", Effect = "Swap the Might of two units at the same battlefield this turn.", GameplayKeywords = ["Action", "Hidden"] },
            new RiftboundCard { Id = 30, Name = "Last Rites", Type = "Gear", Effect = "[Equip] — [Chaos], Recycle 2 cards from your trash.", GameplayKeywords = ["Equip"] },
            new RiftboundCard { Id = 31, Name = "Obelisk of Power", Type = "Battlefield", Effect = "At the start of each Player's first Beginning Phase, that player channels 1 rune." },
            new RiftboundCard { Id = 32, Name = "Reaver's Row", Type = "Battlefield", Effect = "When you defend here, you may move a friendly unit here to base." },
            new RiftboundCard { Id = 33, Name = "Targon's Peak", Type = "Battlefield", Effect = "When you conquer here, ready up to 2 runes at the end of this turn." },
            new RiftboundCard { Id = 34, Name = "Trifarian War Camp", Type = "Battlefield", Effect = "Units here have +1 [Might]." },
            new RiftboundCard { Id = 35, Name = "Zaun Warrens", Type = "Battlefield", Effect = "When you conquer here, discard 1, then draw 1." },
        ];
    }
}
