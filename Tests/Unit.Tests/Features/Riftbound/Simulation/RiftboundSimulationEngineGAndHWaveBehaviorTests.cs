using Application.Features.Riftbound.Simulation.Definitions;
using Application.Features.Riftbound.Simulation.Effects;
using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Unit.Tests.Features.Riftbound.Simulation;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.RiftboundTests)]
public sealed class RiftboundSimulationEngineGAndHWaveBehaviorTests
    : RiftboundSimulationEngineBehaviorTestBase
{
    [Fact]
    public void GAndHWaveCards_ResolveToSupportedTemplates()
    {
        foreach (var card in BuildWaveCards())
        {
            var template = RiftboundEffectTemplateResolver.Resolve(card);
            Assert.True(template.Supported, $"{card.Name} resolved as unsupported.");
            Assert.NotEqual("unsupported", template.TemplateId);
        }
    }

    [Fact]
    public void GarenCommander_BuffsOtherFriendlyUnitsAtSameBattlefield()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026032501);
        var player = session.Players[0];
        var opponent = session.Players[1];
        ResetPlayer(player);
        ResetPlayer(opponent);

        session.TurnPlayerIndex = 1;
        var battlefield = session.Battlefields[0];
        var commander = BuildCardInstance(
            new RiftboundCard { Id = 981001, Name = "Garen, Commander", Type = "Unit", Cost = 0, Power = 0, Might = 5 },
            0,
            0
        );
        var ally = BuildUnit(0, 0, "Ally 3", 3);
        battlefield.Units.Add(commander);
        battlefield.Units.Add(ally);

        var gust = BuildCardInstance(
            new RiftboundCard { Id = 981002, Name = "Gust", Type = "Spell", Cost = 0, Power = 0, GameplayKeywords = ["Reaction"] },
            1,
            1
        );
        opponent.HandZone.Cards.Add(gust);

        Assert.DoesNotContain(
            engine.GetLegalActions(session),
            x =>
                x.ActionId.Contains(gust.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.Contains(ally.InstanceId.ToString(), StringComparison.Ordinal)
        );
    }

    [Fact]
    public void GarenMightOfDemacia_OnConquerWithFourFriendlyUnits_DrawsTwo()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026032502);
        var player = session.Players[0];
        var opponent = session.Players[1];
        ResetPlayer(player);
        ResetPlayer(opponent);

        var legend = BuildCardInstance(
            new RiftboundCard { Id = 981010, Name = "Garen, Might of Demacia", Type = "Legend", Cost = 0, Power = 0 },
            0,
            0
        );
        player.LegendZone.Cards.Add(legend);
        player.MainDeckZone.Cards.Add(BuildUnit(0, 0, "Draw A", 1));
        player.MainDeckZone.Cards.Add(BuildUnit(0, 0, "Draw B", 1));

        var attacker = BuildUnit(0, 0, "Attacker", 3);
        var allyA = BuildUnit(0, 0, "Ally A", 2);
        var allyB = BuildUnit(0, 0, "Ally B", 2);
        var allyC = BuildUnit(0, 0, "Ally C", 2);
        player.BaseZone.Cards.Add(attacker);
        session.Battlefields[0].Units.AddRange([allyA, allyB, allyC]);
        session.Battlefields[0].Units.Add(BuildUnit(1, 1, "Defender", 1));
        session.Battlefields[0].ControlledByPlayerIndex = 1;

        var moveAction = engine.GetLegalActions(session)
            .First(x =>
                x.ActionType == RiftboundActionType.StandardMove
                && x.ActionId.Contains($"move-{attacker.InstanceId}", StringComparison.Ordinal)
                && x.ActionId.Contains("-to-bf-0", StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, moveAction).Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);

        Assert.True(player.HandZone.Cards.Count >= 2);
    }

    [Fact]
    public void Gearhead_DoublesAttachedEquipmentBaseBonus()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026032503);
        var player = session.Players[0];
        var opponent = session.Players[1];
        ResetPlayer(player);
        ResetPlayer(opponent);

        var gearhead = BuildCardInstance(
            new RiftboundCard { Id = 981020, Name = "Gearhead", Type = "Unit", Cost = 0, Power = 0, Might = 3 },
            0,
            0
        );
        var doransBlade = BuildCardInstance(
            new RiftboundCard
            {
                Id = 981021,
                Name = "Doran's Blade",
                Type = "Gear",
                Cost = 0,
                Power = 0,
                GameplayKeywords = ["Equip"],
                Effect = "+2 [Might]",
            },
            0,
            0
        );
        doransBlade.AttachedToInstanceId = gearhead.InstanceId;
        player.BaseZone.Cards.Add(gearhead);
        player.BaseZone.Cards.Add(doransBlade);

        var enemy = BuildUnit(1, 1, "Enemy 7", 7);
        session.Battlefields[0].Units.Add(enemy);
        var challenge = BuildCardInstance(
            new RiftboundCard { Id = 981022, Name = "Challenge", Type = "Spell", Cost = 0, Power = 0, GameplayKeywords = ["Action"] },
            0,
            0
        );
        player.HandZone.Cards.Add(challenge);

        var action = engine.GetLegalActions(session)
            .First(x =>
                x.ActionId.Contains(challenge.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.Contains(gearhead.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.Contains(enemy.InstanceId.ToString(), StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, action).Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);

        Assert.Contains(opponent.TrashZone.Cards, x => x.InstanceId == enemy.InstanceId);
    }

    [Fact]
    public void GemcraftSeer_OnPlay_CreatesVisionChoiceAndCanRecycleTopCard()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026032504);
        var player = session.Players[0];
        ResetPlayer(player);

        var top = BuildUnit(0, 0, "Top Card", 1);
        var second = BuildUnit(0, 0, "Second Card", 1);
        player.MainDeckZone.Cards.Add(top);
        player.MainDeckZone.Cards.Add(second);

        var seer = BuildCardInstance(
            new RiftboundCard
            {
                Id = 981030,
                Name = "Gemcraft Seer",
                Type = "Unit",
                Cost = 0,
                Power = 0,
                Might = 3,
                GameplayKeywords = ["Vision"],
            },
            0,
            0
        );
        player.HandZone.Cards.Add(seer);

        var play = engine.GetLegalActions(session)
            .First(x =>
                x.ActionType == RiftboundActionType.PlayCard
                && x.ActionId.Contains(seer.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.EndsWith("-to-base", StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, play).Succeeded);

        var recycle = engine.GetLegalActions(session)
            .First(x => x.ActionId.Contains("choose-gemcraft-seer-recycle", StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, recycle).Succeeded);
        Assert.Equal(second.InstanceId, player.MainDeckZone.Cards[0].InstanceId);
    }

    [Fact]
    public void GetExcited_DiscardsChosenCardAndDealsDiscardCostDamage()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026032505);
        var player = session.Players[0];
        var opponent = session.Players[1];
        ResetPlayer(player);
        ResetPlayer(opponent);

        var discard = BuildCardInstance(
            new RiftboundCard { Id = 981040, Name = "Discard Cost 4", Type = "Spell", Cost = 4, Power = 0 },
            0,
            0
        );
        var getExcited = BuildCardInstance(
            new RiftboundCard { Id = 981041, Name = "Get Excited!", Type = "Spell", Cost = 0, Power = 0, GameplayKeywords = ["Action"] },
            0,
            0
        );
        player.HandZone.Cards.Add(discard);
        player.HandZone.Cards.Add(getExcited);
        var target = BuildUnit(1, 1, "Target", 4);
        session.Battlefields[0].Units.Add(target);

        var action = engine.GetLegalActions(session)
            .First(x =>
                x.ActionId.Contains(getExcited.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.Contains($"{GetExcitedEffect.DiscardMarker}{discard.InstanceId}", StringComparison.Ordinal)
                && x.ActionId.Contains(target.InstanceId.ToString(), StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, action).Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);

        Assert.Contains(player.TrashZone.Cards, x => x.InstanceId == discard.InstanceId);
        Assert.Contains(opponent.TrashZone.Cards, x => x.InstanceId == target.InstanceId);
    }

    [Fact]
    public void GlascMixologist_Deathknell_PlaysEligibleUnitFromTrash()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026032506);
        var player = session.Players[0];
        ResetPlayer(player);

        var mixologist = BuildCardInstance(
            new RiftboundCard { Id = 981050, Name = "Glasc Mixologist", Type = "Unit", Cost = 0, Power = 0, Might = 5 },
            0,
            0
        );
        mixologist.MarkedDamage = 5;
        player.BaseZone.Cards.Add(mixologist);
        player.BaseZone.Cards.Add(BuildRuneInstance(981051, "Order Rune", "Order", 0));
        var trashUnit = BuildCardInstance(
            new RiftboundCard { Id = 981052, Name = "Trash Unit", Type = "Unit", Cost = 3, Power = 1, Might = 3 },
            0,
            0
        );
        player.TrashZone.Cards.Add(trashUnit);

        var activate = engine.GetLegalActions(session)
            .First(x => x.ActionType == RiftboundActionType.ActivateRune)
            .ActionId;
        Assert.True(engine.ApplyAction(session, activate).Succeeded);

        Assert.Contains(player.BaseZone.Cards, x => x.InstanceId == trashUnit.InstanceId);
        Assert.DoesNotContain(player.TrashZone.Cards, x => x.InstanceId == trashUnit.InstanceId);
    }

    [Fact]
    public void Gold_Activate_KillsSelfAndAddsGenericRunePower()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026032507);
        var player = session.Players[0];
        ResetPlayer(player);

        var gold = BuildCardInstance(
            new RiftboundCard
            {
                Id = 981055,
                Name = "Gold",
                Type = "Gear",
                Cost = 0,
                Power = 0,
                GameplayKeywords = ["Reaction"],
            },
            0,
            0
        );
        gold.IsExhausted = false;
        player.BaseZone.Cards.Add(gold);

        var activate = engine.GetLegalActions(session)
            .First(x =>
                x.ActionType == RiftboundActionType.ActivateRune
                && x.ActionId.Contains(gold.InstanceId.ToString(), StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, activate).Succeeded);
        Assert.Contains(player.TrashZone.Cards, x => x.InstanceId == gold.InstanceId);
        Assert.Equal(1, ReadPower(player, "__unknown__"));
    }

    [Fact]
    public void GuardianAngel_PreventsDeathAndRecallsUnit()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026032508);
        var player = session.Players[0];
        ResetPlayer(player);

        var unit = BuildUnit(0, 0, "Protected Unit", 4);
        unit.MarkedDamage = 6;
        var angel = BuildCardInstance(
            new RiftboundCard
            {
                Id = 981060,
                Name = "Guardian Angel",
                Type = "Gear",
                Cost = 0,
                Power = 0,
                GameplayKeywords = ["Equip"],
            },
            0,
            0
        );
        angel.AttachedToInstanceId = unit.InstanceId;
        player.BaseZone.Cards.Add(unit);
        player.BaseZone.Cards.Add(angel);
        player.BaseZone.Cards.Add(BuildRuneInstance(981061, "Calm Rune", "Calm", 0));

        var activate = engine.GetLegalActions(session)
            .First(x => x.ActionType == RiftboundActionType.ActivateRune)
            .ActionId;
        Assert.True(engine.ApplyAction(session, activate).Succeeded);

        Assert.Contains(player.HandZone.Cards, x => x.InstanceId == unit.InstanceId);
        Assert.DoesNotContain(player.TrashZone.Cards, x => x.InstanceId == unit.InstanceId);
        Assert.Contains(player.TrashZone.Cards, x => x.InstanceId == angel.InstanceId);
    }

    [Fact]
    public void HallOfLegends_OnConquer_ReadiesExhaustedLegend()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026032509);
        ReplaceBattlefieldName(session, 0, "Hall of Legends");
        var player = session.Players[0];
        var opponent = session.Players[1];
        ResetPlayer(player);
        ResetPlayer(opponent);

        var legend = BuildCardInstance(
            new RiftboundCard { Id = 981070, Name = "Any Legend", Type = "Legend", Cost = 0, Power = 0 },
            0,
            0
        );
        legend.IsExhausted = true;
        player.LegendZone.Cards.Add(legend);
        player.BaseZone.Cards.Add(BuildRuneInstance(981071, "Body Rune", "Body", 0));

        var attacker = BuildUnit(0, 0, "Attacker", 4);
        player.BaseZone.Cards.Add(attacker);
        session.Battlefields[0].Units.Add(BuildUnit(1, 1, "Defender", 1));
        session.Battlefields[0].ControlledByPlayerIndex = 1;

        var move = engine.GetLegalActions(session)
            .First(x =>
                x.ActionType == RiftboundActionType.StandardMove
                && x.ActionId.Contains(attacker.InstanceId.ToString(), StringComparison.Ordinal)
                && x.ActionId.Contains("-to-bf-0", StringComparison.Ordinal))
            .ActionId;
        Assert.True(engine.ApplyAction(session, move).Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);
        Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);

        Assert.False(legend.IsExhausted);
    }

    [Fact]
    public void HallowedTomb_OnHold_ReturnsChosenChampionFromTrashWhenChampionZoneEmpty()
    {
        var engine = new RiftboundSimulationEngine();
        var session = CreateSession(engine, 2026032510);
        ReplaceBattlefieldName(session, 0, "Hallowed Tomb");
        var player = session.Players[0];
        var opponent = session.Players[1];
        ResetPlayer(player);
        ResetPlayer(opponent);

        session.Battlefields[0].ControlledByPlayerIndex = 0;
        var chosenChampion = BuildCardInstance(
            new RiftboundCard { Id = 981080, Name = "Chosen Champion", Type = "Unit", Cost = 0, Power = 0, Might = 4 },
            0,
            0
        );
        chosenChampion.EffectData["isChosenChampion"] = "true";
        player.TrashZone.Cards.Add(chosenChampion);
        player.MainDeckZone.Cards.Add(BuildUnit(0, 0, "Draw 1", 1));
        opponent.MainDeckZone.Cards.Add(BuildUnit(1, 1, "Draw 2", 1));

        Assert.True(engine.ApplyAction(session, "end-turn").Succeeded);
        Assert.True(engine.ApplyAction(session, "end-turn").Succeeded);

        Assert.Contains(player.ChampionZone.Cards, x => x.InstanceId == chosenChampion.InstanceId);
    }

    private static GameSession CreateSession(RiftboundSimulationEngine engine, int seed)
    {
        return engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                seed,
                RiftboundSimulationTestData.BuildDeck(seed + 1, "Order"),
                RiftboundSimulationTestData.BuildDeck(seed + 2, "Chaos")
            )
        );
    }

    private static void ReplaceBattlefieldName(GameSession session, int battlefieldIndex, string battlefieldName)
    {
        var current = session.Battlefields[battlefieldIndex];
        session.Battlefields[battlefieldIndex] = new BattlefieldState
        {
            CardId = current.CardId,
            Name = battlefieldName,
            Index = current.Index,
            ControlledByPlayerIndex = current.ControlledByPlayerIndex,
            ContestedByPlayerIndex = current.ContestedByPlayerIndex,
            IsShowdownStaged = current.IsShowdownStaged,
            IsCombatStaged = current.IsCombatStaged,
            Units = current.Units,
            Gear = current.Gear,
            HiddenCards = current.HiddenCards,
        };
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
            new RiftboundCard { Id = 181, Name = "Garen, Commander", Type = "Unit", Effect = "Other friendly units have +1 [Might] here." },
            new RiftboundCard { Id = 182, Name = "Garen, Might of Demacia", Type = "Legend", Effect = "When you conquer, if you have 4+ units at that battlefield, draw 2." },
            new RiftboundCard { Id = 183, Name = "Garen, Rugged", Type = "Unit", Effect = "[Assault 2], [Shield 2]", GameplayKeywords = ["Assault", "Shield"] },
            new RiftboundCard { Id = 184, Name = "Gearhead", Type = "Unit", Effect = "Each Equipment attached to me gives double its base Might bonus.", GameplayKeywords = ["Accelerate"] },
            new RiftboundCard { Id = 185, Name = "Gem Jammer", Type = "Unit", Effect = "When you play me, give a unit [GANKING] this turn.", GameplayKeywords = ["Ganking"] },
            new RiftboundCard { Id = 186, Name = "Gemcraft Seer", Type = "Unit", Effect = "[VISION] Other friendly units have [VISION].", GameplayKeywords = ["Vision"] },
            new RiftboundCard { Id = 187, Name = "Gentlemen's Duel", Type = "Spell", Effect = "Give a friendly unit +3 [Might] this turn. Then choose an enemy unit. They deal damage equal to their Mights to each other.", GameplayKeywords = ["Action"] },
            new RiftboundCard { Id = 188, Name = "Get Excited!", Type = "Spell", Effect = "Discard 1. Deal its Energy cost as damage to a unit at a battlefield.", GameplayKeywords = ["Action"] },
            new RiftboundCard { Id = 189, Name = "Glasc Mixologist", Type = "Unit", Effect = "[Deathknell] - You may play a unit with cost no more than [3] and no more than [Rune] from your trash, ignoring its cost.", GameplayKeywords = ["Deathknell"] },
            new RiftboundCard { Id = 190, Name = "Gold", Type = "Gear", Effect = "Kill this, [Tap]: [Reaction] - [Add] [Rune].", GameplayKeywords = ["Reaction"] },
            new RiftboundCard { Id = 191, Name = "Grand Strategem", Type = "Spell", Effect = "Give friendly units +5 [Might] this turn.", GameplayKeywords = ["Action"] },
            new RiftboundCard { Id = 192, Name = "Grove of the God-Willow", Type = "Battlefield", Effect = "When you hold here, draw 1." },
            new RiftboundCard { Id = 308, Name = "Obelisk of Power", Type = "Battlefield", Effect = "At the start of each Player's first Beginning Phase, that player channels 1 rune." },
            new RiftboundCard { Id = 193, Name = "Guardian Angel", Type = "Gear", Effect = "If I would die, kill Guardian Angel instead. Heal me, Exhaust me, and recall me. +1 [Might]", GameplayKeywords = ["Equip"] },
            new RiftboundCard { Id = 194, Name = "Guardian of the Passage", Type = "Unit", Effect = "When I hold, you may return a unit or gear from your trash to your hand." },
            new RiftboundCard { Id = 195, Name = "Guards!", Type = "Spell", Effect = "Play a 2 [Might] Sand Soldier unit token. You may pay [Order] to ready it.", GameplayKeywords = ["Hidden"] },
            new RiftboundCard { Id = 196, Name = "Guerilla Warfare", Type = "Spell", Effect = "Return up to two cards with [HIDDEN] from your trash to your hand. You can hide ignoring costs this turn.", GameplayKeywords = ["Hidden"] },
            new RiftboundCard { Id = 197, Name = "Gust", Type = "Spell", Effect = "Return a unit at a battlefield with 3 [Might] or less to its owner's hand.", GameplayKeywords = ["Reaction"] },
            new RiftboundCard { Id = 198, Name = "Hall of Legends", Type = "Battlefield", Effect = "When you conquer here, you may pay [1] to ready your legend." },
            new RiftboundCard { Id = 199, Name = "Hallowed Tomb", Type = "Battlefield", Effect = "When you hold here, you may return your Chosen Champion from your trash to your Champion Zone if it is empty." },
        ];
    }
}
