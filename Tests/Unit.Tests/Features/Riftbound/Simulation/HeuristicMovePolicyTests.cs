using Application.Features.Riftbound.Simulation.Engine;
using Application.Features.Riftbound.Simulation.Policies;
using Domain.Simulation;

namespace Unit.Tests.Features.Riftbound.Simulation;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.RiftboundTests)]
public class HeuristicMovePolicyTests
{
    [Fact]
    public async Task ChooseActionIdAsync_PrefersPlayToBattlefield()
    {
        var policy = new HeuristicMovePolicy();
        var context = new RiftboundMovePolicyContext(
            BuildSession(),
            0,
            new[]
            {
                new RiftboundLegalAction("end-turn", RiftboundActionType.EndTurn, 0, "End turn"),
                new RiftboundLegalAction(
                    "activate-rune-aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
                    RiftboundActionType.ActivateRune,
                    0,
                    "Activate"
                ),
                new RiftboundLegalAction(
                    "play-bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb-to-base",
                    RiftboundActionType.PlayCard,
                    0,
                    "Play to base"
                ),
                new RiftboundLegalAction(
                    "play-cccccccc-cccc-cccc-cccc-cccccccccccc-to-bf-0",
                    RiftboundActionType.PlayCard,
                    0,
                    "Play to bf"
                ),
            }
        );

        var selected = await policy.ChooseActionIdAsync(context, CancellationToken.None);

        Assert.Equal("play-cccccccc-cccc-cccc-cccc-cccccccccccc-to-bf-0", selected);
    }

    [Fact]
    public async Task ChooseActionIdAsync_IgnoresActionsFromOtherPlayers()
    {
        var policy = new HeuristicMovePolicy();
        var context = new RiftboundMovePolicyContext(
            BuildSession(),
            0,
            new[]
            {
                new RiftboundLegalAction(
                    "play-dddddddd-dddd-dddd-dddd-dddddddddddd-to-bf-0",
                    RiftboundActionType.PlayCard,
                    1,
                    "Other player action"
                ),
                new RiftboundLegalAction("end-turn", RiftboundActionType.EndTurn, 0, "End turn"),
            }
        );

        var selected = await policy.ChooseActionIdAsync(context, CancellationToken.None);

        Assert.Equal("end-turn", selected);
    }

    [Fact]
    public async Task ChooseActionIdAsync_ReturnsNull_WhenNoLegalActionForCurrentPlayer()
    {
        var policy = new HeuristicMovePolicy();
        var context = new RiftboundMovePolicyContext(
            BuildSession(),
            0,
            new[]
            {
                new RiftboundLegalAction(
                    "play-eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee-to-bf-0",
                    RiftboundActionType.PlayCard,
                    1,
                    "Other player action"
                ),
            }
        );

        var selected = await policy.ChooseActionIdAsync(context, CancellationToken.None);

        Assert.Null(selected);
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
                BuildPlayer(0, "heuristic"),
                BuildPlayer(1, "heuristic"),
            ],
            Battlefields = [],
            Showdown = new ShowdownState(),
            Combat = new CombatState(),
            Chain = [],
            EffectContexts = [],
            UsedScoringKeys = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase),
        };
    }

    private static PlayerState BuildPlayer(int playerIndex, string policy)
    {
        return new PlayerState
        {
            PlayerIndex = playerIndex,
            UserId = playerIndex + 1,
            DeckId = playerIndex + 10,
            Policy = policy,
            Score = 0,
            FirstTurnExtraChannelBonus = false,
            MainDeckZone = new ZoneState { Name = "Main", Cards = [] },
            RuneDeckZone = new ZoneState { Name = "Runes", Cards = [] },
            HandZone = new ZoneState { Name = "Hand", Cards = [] },
            BaseZone = new ZoneState { Name = "Base", Cards = [] },
            TrashZone = new ZoneState { Name = "Trash", Cards = [] },
            ChampionZone = new ZoneState { Name = "Champion", Cards = [] },
            LegendZone = new ZoneState { Name = "Legend", Cards = [] },
            RunePool = new RunePool(),
        };
    }
}
