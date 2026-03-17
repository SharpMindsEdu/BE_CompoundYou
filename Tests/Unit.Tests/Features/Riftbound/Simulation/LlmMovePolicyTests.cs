using Application.Features.Riftbound.Simulation.Engine;
using Application.Features.Riftbound.Simulation.Policies;
using Domain.Services.Ai;
using Domain.Simulation;
using Microsoft.Extensions.Logging.Abstractions;

namespace Unit.Tests.Features.Riftbound.Simulation;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.RiftboundTests)]
public class RiftboundModelMovePolicyTests
{
    [Fact]
    public async Task ChooseActionIdAsync_FallsBackToHeuristic_WhenModelReturnsInvalidAction()
    {
        var model = new StubRiftboundAiModelService(actionAnswer: "invalid-action");
        var fallback = new HeuristicMovePolicy();
        var sut = new RiftboundModelMovePolicy(
            model,
            fallback,
            NullLogger<RiftboundModelMovePolicy>.Instance
        );

        var context = new RiftboundMovePolicyContext(
            BuildSession(),
            0,
            new[]
            {
                new RiftboundLegalAction(
                    "activate-rune-11111111-1111-1111-1111-111111111111",
                    RiftboundActionType.ActivateRune,
                    0,
                    "Activate rune"
                ),
                new RiftboundLegalAction("end-turn", RiftboundActionType.EndTurn, 0, "End turn"),
            }
        );

        var selected = await sut.ChooseActionIdAsync(context, CancellationToken.None);

        Assert.Equal("activate-rune-11111111-1111-1111-1111-111111111111", selected);
    }

    [Fact]
    public async Task ChooseActionIdAsync_UsesModelAction_WhenItIsLegal()
    {
        var model = new StubRiftboundAiModelService(actionAnswer: "end-turn");
        var fallback = new HeuristicMovePolicy();
        var sut = new RiftboundModelMovePolicy(
            model,
            fallback,
            NullLogger<RiftboundModelMovePolicy>.Instance
        );

        var context = new RiftboundMovePolicyContext(
            BuildSession(),
            0,
            new[]
            {
                new RiftboundLegalAction(
                    "activate-rune-11111111-1111-1111-1111-111111111111",
                    RiftboundActionType.ActivateRune,
                    0,
                    "Activate rune"
                ),
                new RiftboundLegalAction("end-turn", RiftboundActionType.EndTurn, 0, "End turn"),
            }
        );

        var selected = await sut.ChooseActionIdAsync(context, CancellationToken.None);

        Assert.Equal("end-turn", selected);
    }

    [Fact]
    public async Task ChooseActionIdAsync_ReturnsNull_WhenCurrentPlayerHasNoLegalActions()
    {
        var model = new StubRiftboundAiModelService(actionAnswer: "end-turn");
        var fallback = new HeuristicMovePolicy();
        var sut = new RiftboundModelMovePolicy(
            model,
            fallback,
            NullLogger<RiftboundModelMovePolicy>.Instance
        );

        var context = new RiftboundMovePolicyContext(
            BuildSession(),
            0,
            [
                new RiftboundLegalAction("end-turn", RiftboundActionType.EndTurn, 1, "End turn"),
            ]
        );

        var selected = await sut.ChooseActionIdAsync(context, CancellationToken.None);

        Assert.Null(selected);
    }

    [Fact]
    public async Task ChooseActionIdAsync_FallsBackToHeuristic_WhenModelThrows()
    {
        var model = new ThrowingRiftboundAiModelService();
        var fallback = new HeuristicMovePolicy();
        var sut = new RiftboundModelMovePolicy(
            model,
            fallback,
            NullLogger<RiftboundModelMovePolicy>.Instance
        );

        var context = new RiftboundMovePolicyContext(
            BuildSession(),
            0,
            new[]
            {
                new RiftboundLegalAction(
                    "activate-rune-11111111-1111-1111-1111-111111111111",
                    RiftboundActionType.ActivateRune,
                    0,
                    "Activate rune"
                ),
                new RiftboundLegalAction("end-turn", RiftboundActionType.EndTurn, 0, "End turn"),
            }
        );

        var selected = await sut.ChooseActionIdAsync(context, CancellationToken.None);

        Assert.Equal("activate-rune-11111111-1111-1111-1111-111111111111", selected);
    }

    [Fact]
    public async Task ChooseActionIdAsync_UsesReactionEndpoint_WhenReactionActionIsPresent()
    {
        var model = new StubRiftboundAiModelService(
            actionAnswer: "end-turn",
            reactionAnswer: "pass-focus"
        );
        var fallback = new HeuristicMovePolicy();
        var sut = new RiftboundModelMovePolicy(
            model,
            fallback,
            NullLogger<RiftboundModelMovePolicy>.Instance
        );

        var context = new RiftboundMovePolicyContext(
            BuildSession(),
            0,
            new[]
            {
                new RiftboundLegalAction(
                    "pass-focus",
                    RiftboundActionType.PassFocus,
                    0,
                    "Pass focus"
                ),
                new RiftboundLegalAction("end-turn", RiftboundActionType.EndTurn, 0, "End turn"),
            }
        );

        var selected = await sut.ChooseActionIdAsync(context, CancellationToken.None);

        Assert.Equal("pass-focus", selected);
        Assert.Equal(0, model.ActionRequests);
        Assert.Equal(1, model.ReactionRequests);
    }

    private static GameSession BuildSession()
    {
        return new GameSession
        {
            SimulationId = 1,
            RulesetVersion = new RulesetVersion("test"),
            TurnPlayerIndex = 0,
            TurnNumber = 1,
            Phase = RiftboundTurnPhase.Action,
            State = RiftboundTurnState.NeutralOpen,
            Players =
            [
                new PlayerState
                {
                    PlayerIndex = 0,
                    UserId = 10,
                    DeckId = 20,
                    Policy = RiftboundModelMovePolicy.Id,
                    Score = 0,
                    FirstTurnExtraChannelBonus = false,
                    MainDeckZone = new ZoneState { Name = "Main", Cards = [] },
                    RuneDeckZone = new ZoneState { Name = "RuneDeck", Cards = [] },
                    HandZone = new ZoneState { Name = "Hand", Cards = [] },
                    BaseZone = new ZoneState { Name = "Base", Cards = [] },
                    TrashZone = new ZoneState { Name = "Trash", Cards = [] },
                    ChampionZone = new ZoneState { Name = "Champion", Cards = [] },
                    LegendZone = new ZoneState { Name = "Legend", Cards = [] },
                    RunePool = new RunePool(),
                },
                new PlayerState
                {
                    PlayerIndex = 1,
                    UserId = 11,
                    DeckId = 21,
                    Policy = HeuristicMovePolicy.Id,
                    Score = 0,
                    FirstTurnExtraChannelBonus = false,
                    MainDeckZone = new ZoneState { Name = "Main", Cards = [] },
                    RuneDeckZone = new ZoneState { Name = "RuneDeck", Cards = [] },
                    HandZone = new ZoneState { Name = "Hand", Cards = [] },
                    BaseZone = new ZoneState { Name = "Base", Cards = [] },
                    TrashZone = new ZoneState { Name = "Trash", Cards = [] },
                    ChampionZone = new ZoneState { Name = "Champion", Cards = [] },
                    LegendZone = new ZoneState { Name = "Legend", Cards = [] },
                    RunePool = new RunePool(),
                },
            ],
            Battlefields = [],
            Showdown = new ShowdownState(),
            Combat = new CombatState(),
            Chain = [],
            EffectContexts = [],
            UsedScoringKeys = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase),
        };
    }

    private sealed class StubRiftboundAiModelService(string? actionAnswer, string? reactionAnswer = null)
        : IRiftboundAiModelService
    {
        public int ActionRequests { get; private set; }

        public int ReactionRequests { get; private set; }

        public Task<string?> SelectActionIdAsync(
            RiftboundActionDecisionRequest request,
            CancellationToken cancellationToken = default
        )
        {
            ActionRequests++;
            return Task.FromResult(actionAnswer);
        }

        public Task<string?> SelectReactionIdAsync(
            RiftboundActionDecisionRequest request,
            CancellationToken cancellationToken = default
        )
        {
            ReactionRequests++;
            return Task.FromResult(reactionAnswer ?? actionAnswer);
        }

        public Task<RiftboundDeckBuildProposal?> BuildDeckAsync(
            RiftboundDeckBuildRequest request,
            CancellationToken cancellationToken = default
        )
        {
            return Task.FromResult<RiftboundDeckBuildProposal?>(null);
        }
    }

    private sealed class ThrowingRiftboundAiModelService : IRiftboundAiModelService
    {
        public Task<string?> SelectActionIdAsync(
            RiftboundActionDecisionRequest request,
            CancellationToken cancellationToken = default
        )
        {
            throw new InvalidOperationException("Simulated model failure");
        }

        public Task<string?> SelectReactionIdAsync(
            RiftboundActionDecisionRequest request,
            CancellationToken cancellationToken = default
        )
        {
            throw new InvalidOperationException("Simulated model failure");
        }

        public Task<RiftboundDeckBuildProposal?> BuildDeckAsync(
            RiftboundDeckBuildRequest request,
            CancellationToken cancellationToken = default
        )
        {
            throw new InvalidOperationException("Simulated model failure");
        }
    }
}

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.RiftboundTests)]
public class LlmMovePolicyTests
{
    [Fact]
    public async Task ChooseActionIdAsync_DelegatesToRiftboundModelPolicy()
    {
        var model = new StubRiftboundAiModelService(actionAnswer: "end-turn");
        var fallback = new HeuristicMovePolicy();
        var core = new RiftboundModelMovePolicy(
            model,
            fallback,
            NullLogger<RiftboundModelMovePolicy>.Instance
        );
        var sut = new LlmMovePolicy(core);

        var context = new RiftboundMovePolicyContext(
            BuildSession(),
            0,
            [
                new RiftboundLegalAction("end-turn", RiftboundActionType.EndTurn, 0, "End turn"),
            ]
        );

        var selected = await sut.ChooseActionIdAsync(context, CancellationToken.None);

        Assert.Equal("end-turn", selected);
        Assert.Equal(LlmMovePolicy.Id, sut.PolicyId);
    }

    private static GameSession BuildSession()
    {
        return new GameSession
        {
            SimulationId = 1,
            RulesetVersion = new RulesetVersion("test"),
            TurnPlayerIndex = 0,
            TurnNumber = 1,
            Phase = RiftboundTurnPhase.Action,
            State = RiftboundTurnState.NeutralOpen,
            Players =
            [
                new PlayerState
                {
                    PlayerIndex = 0,
                    UserId = 10,
                    DeckId = 20,
                    Policy = LlmMovePolicy.Id,
                    Score = 0,
                    FirstTurnExtraChannelBonus = false,
                    MainDeckZone = new ZoneState { Name = "Main", Cards = [] },
                    RuneDeckZone = new ZoneState { Name = "RuneDeck", Cards = [] },
                    HandZone = new ZoneState { Name = "Hand", Cards = [] },
                    BaseZone = new ZoneState { Name = "Base", Cards = [] },
                    TrashZone = new ZoneState { Name = "Trash", Cards = [] },
                    ChampionZone = new ZoneState { Name = "Champion", Cards = [] },
                    LegendZone = new ZoneState { Name = "Legend", Cards = [] },
                    RunePool = new RunePool(),
                },
                new PlayerState
                {
                    PlayerIndex = 1,
                    UserId = 11,
                    DeckId = 21,
                    Policy = HeuristicMovePolicy.Id,
                    Score = 0,
                    FirstTurnExtraChannelBonus = false,
                    MainDeckZone = new ZoneState { Name = "Main", Cards = [] },
                    RuneDeckZone = new ZoneState { Name = "RuneDeck", Cards = [] },
                    HandZone = new ZoneState { Name = "Hand", Cards = [] },
                    BaseZone = new ZoneState { Name = "Base", Cards = [] },
                    TrashZone = new ZoneState { Name = "Trash", Cards = [] },
                    ChampionZone = new ZoneState { Name = "Champion", Cards = [] },
                    LegendZone = new ZoneState { Name = "Legend", Cards = [] },
                    RunePool = new RunePool(),
                },
            ],
            Battlefields = [],
            Showdown = new ShowdownState(),
            Combat = new CombatState(),
            Chain = [],
            EffectContexts = [],
            UsedScoringKeys = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase),
        };
    }

    private sealed class StubRiftboundAiModelService(string? actionAnswer) : IRiftboundAiModelService
    {
        public Task<string?> SelectActionIdAsync(
            RiftboundActionDecisionRequest request,
            CancellationToken cancellationToken = default
        )
        {
            return Task.FromResult(actionAnswer);
        }

        public Task<string?> SelectReactionIdAsync(
            RiftboundActionDecisionRequest request,
            CancellationToken cancellationToken = default
        )
        {
            return Task.FromResult(actionAnswer);
        }

        public Task<RiftboundDeckBuildProposal?> BuildDeckAsync(
            RiftboundDeckBuildRequest request,
            CancellationToken cancellationToken = default
        )
        {
            return Task.FromResult<RiftboundDeckBuildProposal?>(null);
        }
    }
}
