using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Unit.Tests.Features.Riftbound.Simulation;

[Trait("category", ServiceTestCategories.UnitTests)]
public sealed class RiftboundSimulationEngineApheliosExaltedBehaviorTests : RiftboundSimulationEngineBehaviorTestBase
{
    [Fact]
    public void ApheliosExalted_OnEquipmentAttach_UsesEachChoiceOnlyOncePerTurn()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031717,
                RiftboundSimulationTestData.BuildDeck(9770, "Calm"),
                RiftboundSimulationTestData.BuildDeck(9771, "Order")
            )
        );

        var player = session.Players[0];
        player.HandZone.Cards.Clear();
        player.BaseZone.Cards.Clear();

        var aphelios = BuildCardInstance(
            new RiftboundCard
            {
                Id = 9770_100,
                Name = "Aphelios, Exalted",
                Type = "Unit",
                Supertype = "Champion",
                Cost = 0,
                Power = 1,
                Might = 4,
                Color = ["Calm"],
                Effect = "When you attach an Equipment to me, choose one that hasn't been chosen this turn.",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        Assert.Equal("named.aphelios-exalted", aphelios.EffectTemplateId);
        player.BaseZone.Cards.Add(aphelios);

        var runeA = BuildRuneInstance(9770_101, "Calm Rune", "Calm", ownerPlayer: 0);
        var runeB = BuildRuneInstance(9770_102, "Mind Rune", "Mind", ownerPlayer: 0);
        runeA.IsExhausted = true;
        runeB.IsExhausted = true;
        player.BaseZone.Cards.Add(runeA);
        player.BaseZone.Cards.Add(runeB);
        player.BaseZone.Cards.Add(BuildUnit(0, 0, "Ally", 2));

        for (var i = 0; i < 3; i += 1)
        {
            player.HandZone.Cards.Add(
                BuildCardInstance(
                    new RiftboundCard
                    {
                        Id = 9770_200 + i,
                        Name = $"Equip {i}",
                        Type = "Gear",
                        Cost = 0,
                        Power = 0,
                        Color = ["Calm"],
                        GameplayKeywords = ["Equip"],
                        Effect = "[Equip]",
                    },
                    ownerPlayer: 0,
                    controllerPlayer: 0
                )
            );
        }
        Assert.All(
            player.HandZone.Cards,
            gear => Assert.Equal("gear.attach-friendly-unit", gear.EffectTemplateId)
        );

        foreach (var gear in player.HandZone.Cards.ToList())
        {
            var legalGearAction = engine
                .GetLegalActions(session)
                .First(a =>
                    a.ActionType == RiftboundActionType.PlayCard
                    && a.ActionId.Contains(gear.InstanceId.ToString(), StringComparison.Ordinal)
                    && a.ActionId.Contains(aphelios.InstanceId.ToString(), StringComparison.Ordinal)
                )
                .ActionId;
            var playGearResult = engine.ApplyAction(session, legalGearAction);
            Assert.True(
                playGearResult.Succeeded,
                string.Join(" | ", playGearResult.LegalActions.Select(x => x.ActionId))
            );
            Assert.Equal(aphelios.InstanceId, gear.AttachedToInstanceId);
            Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);
            Assert.True(engine.ApplyAction(session, "pass-focus").Succeeded);
        }

        var choices = session.EffectContexts
            .Where(c => c.Source == "Aphelios, Exalted" && c.Timing == "WhenEquipAttached")
            .Select(c => c.Metadata.TryGetValue("choice", out var choice) ? choice : string.Empty)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();
        Assert.True(
            choices.Count == 3,
            string.Join(
                " | ",
                session.EffectContexts.Select(c => $"{c.Source}:{c.Timing}")
            )
        );
        Assert.Equal(3, choices.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }
}

