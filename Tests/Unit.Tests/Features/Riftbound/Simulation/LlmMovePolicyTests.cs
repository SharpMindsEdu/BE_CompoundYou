using Application.Features.Riftbound.Simulation.Engine;
using Application.Features.Riftbound.Simulation.Policies;
using Domain.Services.Ai;
using Domain.Simulation;
using Microsoft.Extensions.Logging.Abstractions;

namespace Unit.Tests.Features.Riftbound.Simulation;

public class LlmMovePolicyTests
{
    [Fact]
    public async Task ChooseActionIdAsync_FallsBackToHeuristic_WhenLlmReturnsInvalidAction()
    {
        var llm = new StubAiService("invalid-action");
        var fallback = new HeuristicMovePolicy();
        var sut = new LlmMovePolicy(llm, fallback, NullLogger<LlmMovePolicy>.Instance);

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
    public async Task ChooseActionIdAsync_UsesLlmAction_WhenItIsLegal()
    {
        var llm = new StubAiService("end-turn");
        var fallback = new HeuristicMovePolicy();
        var sut = new LlmMovePolicy(llm, fallback, NullLogger<LlmMovePolicy>.Instance);

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
        var llm = new StubAiService("end-turn");
        var fallback = new HeuristicMovePolicy();
        var sut = new LlmMovePolicy(llm, fallback, NullLogger<LlmMovePolicy>.Instance);

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
    public async Task ChooseActionIdAsync_FallsBackToHeuristic_WhenLlmThrows()
    {
        var llm = new ThrowingAiService();
        var fallback = new HeuristicMovePolicy();
        var sut = new LlmMovePolicy(llm, fallback, NullLogger<LlmMovePolicy>.Instance);

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
                    Policy = "llm",
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
                    Policy = "heuristic",
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

    private sealed class StubAiService(string? answer) : IAiService
    {
        public Task<string?> SelectActionIdAsync(
            string prompt,
            IReadOnlyCollection<string> legalActionIds,
            CancellationToken cancellationToken = default
        )
        {
            return Task.FromResult(answer);
        }
    }

    private sealed class ThrowingAiService : IAiService
    {
        public Task<string?> SelectActionIdAsync(
            string prompt,
            IReadOnlyCollection<string> legalActionIds,
            CancellationToken cancellationToken = default
        )
        {
            throw new InvalidOperationException("Simulated AI failure");
        }
    }
}
