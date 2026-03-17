using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Unit.Tests.Features.Riftbound.Simulation;

[Trait("category", ServiceTestCategories.UnitTests)]
public sealed class RiftboundSimulationEngineFaithfulManufactorBehaviorTests : RiftboundSimulationEngineBehaviorTestBase
{
    [Fact]
    public void FaithfulManufactor_OnPlay_CreatesRecruitTokenAtSameLocation()
    {
        var engine = new RiftboundSimulationEngine();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                1,
                9,
                2026031719,
                RiftboundSimulationTestData.BuildDeck(9790, "Order"),
                RiftboundSimulationTestData.BuildDeck(9791, "Chaos")
            )
        );

        var player = session.Players[0];
        player.HandZone.Cards.Clear();
        player.BaseZone.Cards.Clear();

        var manufactor = BuildCardInstance(
            new RiftboundCard
            {
                Id = 9790_100,
                Name = "Faithful Manufactor",
                Type = "Unit",
                Cost = 0,
                Power = 0,
                Might = 2,
                Color = ["Order"],
                Effect = "When you play me, play a 1 [Might] Recruit unit token here.",
            },
            ownerPlayer: 0,
            controllerPlayer: 0
        );
        player.HandZone.Cards.Add(manufactor);

        var playAction = engine
            .GetLegalActions(session)
            .First(a =>
                a.ActionType == RiftboundActionType.PlayCard
                && a.ActionId.Contains(manufactor.InstanceId.ToString(), StringComparison.Ordinal)
                && a.ActionId.Contains("-to-bf-", StringComparison.Ordinal)
            )
            .ActionId;
        Assert.True(engine.ApplyAction(session, playAction).Succeeded);

        Assert.Contains(
            session.Battlefields.SelectMany(x => x.Units),
            x => string.Equals(x.Name, "Recruit Token", StringComparison.OrdinalIgnoreCase)
        );
    }
}

