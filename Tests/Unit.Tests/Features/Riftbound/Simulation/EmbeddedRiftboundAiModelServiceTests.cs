using Domain.Services.Ai;
using Infrastructure.Services.Ai;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Unit.Tests.Features.Riftbound.Simulation;

[Trait("category", ServiceTestCategories.UnitTests)]
public class EmbeddedRiftboundAiModelServiceTests
{
    [Fact]
    public async Task TrainFromEpisodeAsync_PrefersWinningAction_ForKnownState()
    {
        var sut = BuildSut();
        var request = BuildDecisionRequest();
        var ct = TestContext.Current.CancellationToken;

        for (var i = 0; i < 12; i++)
        {
            await sut.TrainFromEpisodeAsync(
                new RiftboundAiEpisode(
                    Source: "unit-test",
                    SimulationId: 1,
                    WinnerPlayerIndex: 0,
                    Decisions:
                    [
                        new RiftboundAiDecisionEvent(request, "play-unit", 0),
                        new RiftboundAiDecisionEvent(request, "end-turn", 1),
                    ]
                ),
                ct
            );
        }

        var selected = await sut.SelectActionIdAsync(request, ct);

        Assert.Equal("play-unit", selected);
    }

    [Fact]
    public async Task BuildDeckAsync_PrefersWinningDeckProfile()
    {
        var sut = BuildSut();
        var ct = TestContext.Current.CancellationToken;
        var winningDeck = new RiftboundDeckTrainingOutcome(
            Source: "unit-test",
            RunId: 1,
            DeckId: 10,
            LegendId: 100,
            ChampionId: 201,
            MainDeck:
            [
                new RiftboundDeckTrainingCard(301, 3),
                new RiftboundDeckTrainingCard(302, 3),
                new RiftboundDeckTrainingCard(303, 3),
                new RiftboundDeckTrainingCard(304, 3),
                new RiftboundDeckTrainingCard(305, 3),
                new RiftboundDeckTrainingCard(306, 3),
                new RiftboundDeckTrainingCard(307, 3),
                new RiftboundDeckTrainingCard(308, 3),
                new RiftboundDeckTrainingCard(309, 3),
                new RiftboundDeckTrainingCard(310, 3),
                new RiftboundDeckTrainingCard(311, 3),
                new RiftboundDeckTrainingCard(312, 2),
                new RiftboundDeckTrainingCard(313, 2),
                new RiftboundDeckTrainingCard(314, 1),
                new RiftboundDeckTrainingCard(315, 1),
            ],
            Sideboard:
            [
                new RiftboundDeckTrainingCard(309, 1),
                new RiftboundDeckTrainingCard(310, 1),
                new RiftboundDeckTrainingCard(311, 1),
                new RiftboundDeckTrainingCard(312, 1),
                new RiftboundDeckTrainingCard(313, 1),
                new RiftboundDeckTrainingCard(314, 1),
                new RiftboundDeckTrainingCard(315, 1),
                new RiftboundDeckTrainingCard(316, 1),
            ],
            RuneDeck:
            [
                new RiftboundDeckTrainingCard(401, 6),
                new RiftboundDeckTrainingCard(402, 6),
            ],
            BattlefieldIds: [501, 502, 503],
            IsWinner: true,
            IsDraw: false
        );

        for (var i = 0; i < 60; i++)
        {
            await sut.TrainDeckOutcomeAsync(winningDeck, ct);
        }

        var proposal = await sut.BuildDeckAsync(
            new RiftboundDeckBuildRequest(
                RunId: 2,
                Generation: 0,
                RequestedByUserId: 1,
                Seed: 123,
                MainDeckCardCount: 39,
                SideboardCardCount: 8,
                RuneDeckCardCount: 12,
                BattlefieldCardCount: 3,
                Pool: new RiftboundDeckBuildPool(
                    LegendId: 100,
                    ChampionIds: [201, 202],
                    MainDeckCardIds:
                    [
                        301,
                        302,
                        303,
                        304,
                        305,
                        306,
                        307,
                        308,
                        309,
                        310,
                        311,
                        312,
                        313,
                        314,
                        315,
                        316,
                    ],
                    RuneCardIds: [401, 402],
                    BattlefieldCardIds: [501, 502, 503],
                    Colors: ["RED"]
                )
            ),
            ct
        );

        Assert.NotNull(proposal);
        Assert.Equal(201, proposal!.ChampionId);
        Assert.Equal(39, proposal.MainDeck.Sum(x => x.Quantity));
        Assert.Equal(8, proposal.Sideboard.Sum(x => x.Quantity));
        Assert.Equal(12, proposal.RuneDeck.Sum(x => x.Quantity));
        Assert.Equal(3, proposal.BattlefieldIds.Count);
    }

    private static EmbeddedRiftboundAiModelService BuildSut()
    {
        return new EmbeddedRiftboundAiModelService(
            Options.Create(
                new RiftboundAiModelOptions
                {
                    Enabled = true,
                    TrainingEnabled = true,
                    ExplorationRate = 0d,
                    PersistModelToDisk = false,
                    CaptureTrainingData = false,
                    MinSamplesForDeckBuild = 5,
                }
            ),
            NullLogger<EmbeddedRiftboundAiModelService>.Instance
        );
    }

    private static RiftboundActionDecisionRequest BuildDecisionRequest()
    {
        return new RiftboundActionDecisionRequest(
            SimulationId: 1,
            RulesetVersion: "test",
            TurnNumber: 1,
            Phase: "Action",
            State: "NeutralOpen",
            PlayerIndex: 0,
            OpponentIndex: 1,
            MyScore: 0,
            OpponentScore: 0,
            MyHandCount: 4,
            MyRuneEnergy: 2,
            MyBaseUnits: 1,
            ControlledBattlefields: [],
            LegalActions:
            [
                new RiftboundActionCandidate("play-unit", "PlayCard", "Play unit"),
                new RiftboundActionCandidate("end-turn", "EndTurn", "End turn"),
            ],
            LastOpponentActionId: null,
            DecisionKind: RiftboundDecisionKind.ActionSelection
        );
    }
}
