using Application.Features.Riftbound.Simulation.Engine;
using Application.Features.Riftbound.Simulation.Policies;
using Domain.Simulation;

namespace Unit.Tests.Features.Riftbound.Simulation;

[Trait("category", ServiceTestCategories.UnitTests)]
public class RiftboundGameSimulationTests
{
    [Theory]
    [InlineData(7L)]
    [InlineData(77L)]
    [InlineData(777L)]
    [InlineData(7777L)]
    public async Task Autoplay_CompletesWithinStepBudget_ForDifferentSeeds(long seed)
    {
        var engine = new RiftboundSimulationEngine();
        var policy = new HeuristicMovePolicy();
        var session = engine.CreateSession(
            RiftboundSimulationTestData.BuildSetup(
                simulationId: seed,
                userId: 42,
                seed: seed,
                challengerDeck: RiftboundSimulationTestData.BuildDeck(seed * 10 + 1, "Chaos"),
                opponentDeck: RiftboundSimulationTestData.BuildDeck(seed * 10 + 2, "Order")
            )
        );

        var steps = 0;
        while (session.Phase != RiftboundTurnPhase.Completed && steps < 1_500)
        {
            var legalActions = engine.GetLegalActions(session);
            Assert.NotEmpty(legalActions);
            var activePlayerIndex = legalActions
                .Select(x => x.PlayerIndex)
                .Distinct()
                .Single();

            var selected = await policy.ChooseActionIdAsync(
                new RiftboundMovePolicyContext(session, activePlayerIndex, legalActions),
                CancellationToken.None
            );

            Assert.False(string.IsNullOrWhiteSpace(selected));

            var result = engine.ApplyAction(session, selected!);
            Assert.True(result.Succeeded, result.ErrorMessage);
            AssertSessionInvariants(session);
            steps++;
        }

        Assert.True(session.Phase == RiftboundTurnPhase.Completed, $"Seed {seed} reached step limit.");
        Assert.InRange(steps, 1, 1_500);
        Assert.True(session.Players.Max(p => p.Score) >= 8);
    }

    [Fact]
    public async Task Autoplay_DoesNotYieldIllegalActions_AcrossManySeeds()
    {
        var engine = new RiftboundSimulationEngine();
        var policy = new HeuristicMovePolicy();

        for (var seed = 1; seed <= 15; seed++)
        {
            var session = engine.CreateSession(
                RiftboundSimulationTestData.BuildSetup(
                    simulationId: seed,
                    userId: 7,
                    seed: seed,
                    challengerDeck: RiftboundSimulationTestData.BuildDeck(seed * 100 + 1, "Chaos"),
                    opponentDeck: RiftboundSimulationTestData.BuildDeck(seed * 100 + 2, "Order")
                )
            );

            var step = 0;
            while (session.Phase != RiftboundTurnPhase.Completed && step < 1_200)
            {
                var legalActions = engine.GetLegalActions(session);
                var activePlayerIndex = legalActions
                    .Select(x => x.PlayerIndex)
                    .Distinct()
                    .Single();
                var selected = await policy.ChooseActionIdAsync(
                    new RiftboundMovePolicyContext(session, activePlayerIndex, legalActions),
                    CancellationToken.None
                );

                Assert.Contains(legalActions, a => a.ActionId == selected);

                var result = engine.ApplyAction(session, selected!);
                Assert.True(result.Succeeded, result.ErrorMessage);
                step++;
            }

            Assert.True(
                session.Phase == RiftboundTurnPhase.Completed,
                $"Simulation for seed {seed} did not complete."
            );
        }
    }

    private static void AssertSessionInvariants(GameSession session)
    {
        Assert.All(session.Players, player => Assert.True(player.RunePool.Energy >= 0));
        Assert.All(session.Battlefields, battlefield =>
        {
            Assert.False(battlefield.IsCombatStaged);
            Assert.False(battlefield.IsShowdownStaged);
        });
    }
}
