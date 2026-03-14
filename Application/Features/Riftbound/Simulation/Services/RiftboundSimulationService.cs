using System.Text.Json;
using System.Text.Json.Serialization;
using Application.Features.Riftbound.Simulation.DTOs;
using Application.Features.Riftbound.Simulation.Engine;
using Application.Features.Riftbound.Simulation.Policies;
using Application.Shared;
using Domain.Entities.Riftbound;
using Domain.Repositories;
using Domain.Simulation;
using Domain.Specifications.Riftbound.Decks;

namespace Application.Features.Riftbound.Simulation.Services;

public sealed class RiftboundSimulationService(
    IRepository<RiftboundSimulationRun> simulationRunRepository,
    IRepository<RiftboundSimulationEvent> simulationEventRepository,
    IRiftboundDeckSpecification deckSpecification,
    IRiftboundDeckSimulationReadinessService readinessService,
    IRiftboundSimulationDefinitionRegistry definitionRegistry,
    IRiftboundSimulationEngine simulationEngine,
    IMovePolicyResolver movePolicyResolver
) : IRiftboundSimulationService
{
    private const int MaxDeckTests = 100;
    private const int MaxAutoplaySteps = 2_000;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task<Result<RiftboundDeckSimulationSupportDto>> GetDeckSimulationSupportAsync(
        long userId,
        long deckId,
        CancellationToken cancellationToken
    )
    {
        var deck = await LoadDeckWithAccessAsync(userId, deckId, cancellationToken);
        if (deck is null)
        {
            return Result<RiftboundDeckSimulationSupportDto>.Failure(
                ErrorResults.DeckAccessDenied,
                ResultStatus.NotFound
            );
        }

        var readiness = readinessService.Evaluate(deck);
        var dto = new RiftboundDeckSimulationSupportDto(
            deckId,
            definitionRegistry.RulesetVersion,
            definitionRegistry.RuleCorrections.Select(c => c.Key).ToList(),
            readiness.IsSimulationReady,
            readiness.ValidationIssues,
            readiness.UnsupportedCards
        );

        return Result<RiftboundDeckSimulationSupportDto>.Success(dto);
    }

    public async Task<Result<RiftboundSimulationDto>> CreateSimulationAsync(
        long userId,
        RiftboundSimulationCreateRequest request,
        CancellationToken cancellationToken
    )
    {
        var challengerDeck = await LoadDeckWithAccessAsync(
            userId,
            request.ChallengerDeckId,
            cancellationToken
        );
        var opponentDeck = await LoadDeckWithAccessAsync(
            userId,
            request.OpponentDeckId,
            cancellationToken
        );

        if (challengerDeck is null || opponentDeck is null)
        {
            return Result<RiftboundSimulationDto>.Failure(
                ErrorResults.DeckAccessDenied,
                ResultStatus.NotFound
            );
        }

        var readinessFailure = ValidateDeckReadiness(challengerDeck, opponentDeck);
        if (readinessFailure is not null)
        {
            return Result<RiftboundSimulationDto>.Failure(
                readinessFailure,
                ResultStatus.BadRequest
            );
        }

        var seed = request.Seed.GetValueOrDefault();
        if (seed <= 0)
        {
            seed = Random.Shared.NextInt64(1, long.MaxValue);
        }

        var challengerPolicy = movePolicyResolver.Resolve(request.ChallengerPolicy).PolicyId;
        var opponentPolicy = movePolicyResolver.Resolve(request.OpponentPolicy).PolicyId;

        var run = new RiftboundSimulationRun
        {
            RequestedByUserId = userId,
            ChallengerDeckId = challengerDeck.Id,
            OpponentDeckId = opponentDeck.Id,
            Seed = seed,
            RulesetVersion = definitionRegistry.RulesetVersion,
            Mode = "1v1-duel",
            ChallengerPolicy = challengerPolicy,
            OpponentPolicy = opponentPolicy,
            Status = "running",
            ScoreSummaryJson = "{}",
            SnapshotJson = "{}",
        };

        await simulationRunRepository.Add(run);
        await simulationRunRepository.SaveChanges(cancellationToken);

        var session = simulationEngine.CreateSession(
            new RiftboundSimulationEngineSetup(
                run.Id,
                userId,
                run.Seed,
                run.RulesetVersion,
                challengerDeck,
                opponentDeck,
                run.ChallengerPolicy,
                run.OpponentPolicy
            )
        );
        var legalActions = simulationEngine.GetLegalActions(session);

        ApplySessionSnapshot(run, session);
        simulationRunRepository.Update(run);
        await simulationRunRepository.SaveChanges(cancellationToken);

        var createdEvent = new RiftboundSimulationEvent
        {
            SimulationRunId = run.Id,
            Sequence = 1,
            EventType = "simulation-created",
            PayloadJson = JsonSerializer.Serialize(
                new
                {
                    run.Id,
                    run.Seed,
                    run.RulesetVersion,
                    run.ChallengerDeckId,
                    run.OpponentDeckId,
                    run.ChallengerPolicy,
                    run.OpponentPolicy,
                    legalActionIds = legalActions.Select(x => x.ActionId).ToArray(),
                },
                JsonOptions
            ),
        };
        await simulationEventRepository.Add(createdEvent);
        await simulationEventRepository.SaveChanges(cancellationToken);

        var dto = ToSimulationDto(run, session, legalActions, [createdEvent]);
        return Result<RiftboundSimulationDto>.Success(dto);
    }

    public async Task<Result<RiftboundSimulationDto>> GetSimulationAsync(
        long userId,
        long simulationId,
        CancellationToken cancellationToken
    )
    {
        var loaded = await LoadSimulationAsync(userId, simulationId, cancellationToken);
        if (!loaded.Succeeded)
        {
            return Result<RiftboundSimulationDto>.Failure(loaded.Error!, loaded.Status);
        }

        var legalActions = string.Equals(
            loaded.Run!.Status,
            "running",
            StringComparison.OrdinalIgnoreCase
        )
            ? simulationEngine.GetLegalActions(loaded.Session!)
            : [];

        var dto = ToSimulationDto(loaded.Run!, loaded.Session!, legalActions, loaded.Events!);
        return Result<RiftboundSimulationDto>.Success(dto);
    }

    public async Task<Result<RiftboundSimulationDto>> ApplyActionAsync(
        long userId,
        long simulationId,
        string actionId,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(actionId))
        {
            return Result<RiftboundSimulationDto>.Failure(
                ErrorResults.SimulationActionNotFound,
                ResultStatus.BadRequest
            );
        }

        var loaded = await LoadSimulationAsync(userId, simulationId, cancellationToken);
        if (!loaded.Succeeded)
        {
            return Result<RiftboundSimulationDto>.Failure(loaded.Error!, loaded.Status);
        }

        var run = loaded.Run!;
        var session = loaded.Session!;
        var events = loaded.Events!;

        if (!string.Equals(run.Status, "running", StringComparison.OrdinalIgnoreCase))
        {
            return Result<RiftboundSimulationDto>.Failure(
                ErrorResults.SimulationAlreadyCompleted,
                ResultStatus.Conflict
            );
        }

        var result = simulationEngine.ApplyAction(session, actionId.Trim());
        if (!result.Succeeded)
        {
            return Result<RiftboundSimulationDto>.Failure(
                $"{ErrorResults.SimulationActionNotFound} {result.ErrorMessage}",
                ResultStatus.BadRequest
            );
        }

        var actionEvent = new RiftboundSimulationEvent
        {
            SimulationRunId = run.Id,
            Sequence = events.Count + 1,
            EventType = "action-applied",
            PayloadJson = JsonSerializer.Serialize(
                new
                {
                    actionId = actionId.Trim(),
                    succeeded = result.Succeeded,
                    error = result.ErrorMessage,
                    legalActionIds = result.LegalActions.Select(x => x.ActionId).ToArray(),
                    turn = session.TurnNumber,
                    phase = session.Phase.ToString(),
                    state = session.State.ToString(),
                },
                JsonOptions
            ),
        };

        ApplySessionSnapshot(run, session);
        simulationRunRepository.Update(run);
        await simulationEventRepository.Add(actionEvent);
        await simulationRunRepository.SaveChanges(cancellationToken);
        await simulationEventRepository.SaveChanges(cancellationToken);

        events.Add(actionEvent);
        var dto = ToSimulationDto(run, session, result.LegalActions, events);
        return Result<RiftboundSimulationDto>.Success(dto);
    }

    public async Task<Result<RiftboundSimulationDto>> AutoPlayAsync(
        long userId,
        long simulationId,
        RiftboundSimulationAutoplayRequest request,
        CancellationToken cancellationToken
    )
    {
        var loaded = await LoadSimulationAsync(userId, simulationId, cancellationToken);
        if (!loaded.Succeeded)
        {
            return Result<RiftboundSimulationDto>.Failure(loaded.Error!, loaded.Status);
        }

        var run = loaded.Run!;
        var session = loaded.Session!;
        var events = loaded.Events!;

        if (!string.Equals(run.Status, "running", StringComparison.OrdinalIgnoreCase))
        {
            return Result<RiftboundSimulationDto>.Failure(
                ErrorResults.SimulationAlreadyCompleted,
                ResultStatus.Conflict
            );
        }

        var maxSteps = Math.Clamp(request.MaxSteps, 1, MaxAutoplaySteps);
        var step = 0;
        var heuristic = movePolicyResolver.Resolve(HeuristicMovePolicy.Id);
        IReadOnlyCollection<RiftboundLegalAction> legalActions = [];

        while (step < maxSteps && string.Equals(run.Status, "running", StringComparison.OrdinalIgnoreCase))
        {
            legalActions = simulationEngine.GetLegalActions(session);
            if (legalActions.Count == 0)
            {
                break;
            }

            var activePlayerIndex = legalActions
                .Select(x => x.PlayerIndex)
                .Distinct()
                .Single();
            var configuredPolicyId = activePlayerIndex == 0 ? run.ChallengerPolicy : run.OpponentPolicy;
            var configuredPolicy = movePolicyResolver.Resolve(configuredPolicyId);

            var context = new RiftboundMovePolicyContext(session, activePlayerIndex, legalActions);
            var selectedActionId = await configuredPolicy.ChooseActionIdAsync(
                context,
                cancellationToken
            );

            var legalActionIds = legalActions.Select(x => x.ActionId).ToHashSet(StringComparer.Ordinal);
            var selectedPolicyId = configuredPolicy.PolicyId;
            if (
                string.IsNullOrWhiteSpace(selectedActionId)
                || !legalActionIds.Contains(selectedActionId)
            )
            {
                selectedActionId = await heuristic.ChooseActionIdAsync(context, cancellationToken);
                selectedPolicyId = HeuristicMovePolicy.Id;
            }

            if (string.IsNullOrWhiteSpace(selectedActionId) || !legalActionIds.Contains(selectedActionId))
            {
                break;
            }

            var result = simulationEngine.ApplyAction(session, selectedActionId);
            if (!result.Succeeded)
            {
                break;
            }

            step++;
            legalActions = result.LegalActions;

            events.Add(
                new RiftboundSimulationEvent
                {
                    SimulationRunId = run.Id,
                    Sequence = events.Count + 1,
                    EventType = "autoplay-action",
                    PayloadJson = JsonSerializer.Serialize(
                        new
                        {
                            step,
                            playerIndex = activePlayerIndex,
                            policyId = selectedPolicyId,
                            actionId = selectedActionId,
                            legalActionIds = result.LegalActions.Select(x => x.ActionId).ToArray(),
                        },
                        JsonOptions
                    ),
                }
            );

            ApplySessionSnapshot(run, session);
        }

        events.Add(
            new RiftboundSimulationEvent
            {
                SimulationRunId = run.Id,
                Sequence = events.Count + 1,
                EventType = "autoplay-finished",
                PayloadJson = JsonSerializer.Serialize(
                    new { stepsExecuted = step, run.Status },
                    JsonOptions
                ),
            }
        );

        var newEvents = events.Where(e => e.Id == 0).ToArray();
        if (newEvents.Length > 0)
        {
            await simulationEventRepository.Add(newEvents);
        }

        simulationRunRepository.Update(run);
        await simulationRunRepository.SaveChanges(cancellationToken);
        await simulationEventRepository.SaveChanges(cancellationToken);

        if (string.Equals(run.Status, "running", StringComparison.OrdinalIgnoreCase))
        {
            legalActions = simulationEngine.GetLegalActions(session);
        }
        else
        {
            legalActions = [];
        }

        var dto = ToSimulationDto(run, session, legalActions, events);
        return Result<RiftboundSimulationDto>.Success(dto);
    }

    public async Task<Result<RiftboundDeckTestsResultDto>> RunDeckTestsAsync(
        long userId,
        RiftboundDeckTestsRequest request,
        CancellationToken cancellationToken
    )
    {
        var challengerPolicy = movePolicyResolver.Resolve(request.ChallengerPolicy).PolicyId;
        var opponentPolicy = movePolicyResolver.Resolve(request.OpponentPolicy).PolicyId;
        var seeds = BuildDeckTestSeeds(request.Seeds, request.RunCount);
        var maxSteps = Math.Clamp(request.MaxAutoplaySteps, 1, MaxAutoplaySteps);

        var runs = new List<RiftboundDeckTestRunDto>();
        foreach (var seed in seeds)
        {
            var createResult = await CreateSimulationAsync(
                userId,
                new RiftboundSimulationCreateRequest(
                    request.ChallengerDeckId,
                    request.OpponentDeckId,
                    seed,
                    challengerPolicy,
                    opponentPolicy
                ),
                cancellationToken
            );
            if (!createResult.Succeeded)
            {
                return Result<RiftboundDeckTestsResultDto>.Failure(
                    createResult.ErrorMessage ?? ErrorResults.SimulationDeckNotReady,
                    createResult.Status
                );
            }

            var simulationId = createResult.Data!.SimulationId;
            var autoplayResult = await AutoPlayAsync(
                userId,
                simulationId,
                new RiftboundSimulationAutoplayRequest(maxSteps),
                cancellationToken
            );
            if (!autoplayResult.Succeeded)
            {
                return Result<RiftboundDeckTestsResultDto>.Failure(
                    autoplayResult.ErrorMessage ?? ErrorResults.SimulationAlreadyCompleted,
                    autoplayResult.Status
                );
            }

            var simulation = autoplayResult.Data!;
            runs.Add(
                new RiftboundDeckTestRunDto(
                    simulation.SimulationId,
                    simulation.Seed,
                    simulation.Status,
                    simulation.WinnerPlayerIndex,
                    simulation.Scores
                )
            );
        }

        var challengerWins = runs.Count(r => r.WinnerPlayerIndex == 0);
        var opponentWins = runs.Count(r => r.WinnerPlayerIndex == 1);
        var draws = runs.Count - challengerWins - opponentWins;

        var dto = new RiftboundDeckTestsResultDto(
            request.ChallengerDeckId,
            request.OpponentDeckId,
            definitionRegistry.RulesetVersion,
            challengerPolicy,
            opponentPolicy,
            runs.Count,
            challengerWins,
            opponentWins,
            draws,
            runs
        );

        return Result<RiftboundDeckTestsResultDto>.Success(dto);
    }

    private async Task<RiftboundDeck?> LoadDeckWithAccessAsync(
        long userId,
        long deckId,
        CancellationToken cancellationToken
    )
    {
        return await deckSpecification
            .Reset()
            .IncludeDetails()
            .AccessibleForUser(userId)
            .ByDeckId(deckId)
            .FirstOrDefault(cancellationToken);
    }

    private async Task<SimulationLoadResult> LoadSimulationAsync(
        long userId,
        long simulationId,
        CancellationToken cancellationToken
    )
    {
        var run = await simulationRunRepository.GetById(simulationId);
        if (run is null)
        {
            return SimulationLoadResult.NotFound();
        }

        var hasDeckAccess = await HasDeckAccessAsync(userId, run, cancellationToken);
        if (!hasDeckAccess)
        {
            return SimulationLoadResult.NotFound();
        }

        var session = DeserializeSession(run);
        if (session is null)
        {
            return SimulationLoadResult.Failure(
                "Simulation snapshot could not be restored.",
                ResultStatus.BadRequest
            );
        }

        var events = await simulationEventRepository.ListAll(
            x => x.SimulationRunId == run.Id,
            cancellationToken
        );

        return SimulationLoadResult.Success(run, session, events.OrderBy(x => x.Sequence).ToList());
    }

    private async Task<bool> HasDeckAccessAsync(
        long userId,
        RiftboundSimulationRun run,
        CancellationToken cancellationToken
    )
    {
        if (run.RequestedByUserId == userId)
        {
            return true;
        }

        var challengerVisible = await deckSpecification
            .Reset()
            .AccessibleForUser(userId)
            .ByDeckId(run.ChallengerDeckId)
            .FirstOrDefault(cancellationToken);

        if (challengerVisible is null)
        {
            return false;
        }

        var opponentVisible = await deckSpecification
            .Reset()
            .AccessibleForUser(userId)
            .ByDeckId(run.OpponentDeckId)
            .FirstOrDefault(cancellationToken);

        return opponentVisible is not null;
    }

    private string? ValidateDeckReadiness(RiftboundDeck challengerDeck, RiftboundDeck opponentDeck)
    {
        var challenger = readinessService.Evaluate(challengerDeck);
        if (!challenger.IsSimulationReady)
        {
            return BuildReadinessError("challenger", challenger);
        }

        var opponent = readinessService.Evaluate(opponentDeck);
        if (!opponent.IsSimulationReady)
        {
            return BuildReadinessError("opponent", opponent);
        }

        return null;
    }

    private static string BuildReadinessError(
        string role,
        RiftboundDeckSimulationReadiness readiness
    )
    {
        var issues = readiness.ValidationIssues.Count == 0
            ? "-"
            : string.Join(" | ", readiness.ValidationIssues);
        var unsupported = readiness.UnsupportedCards.Count == 0
            ? "-"
            : string.Join(", ", readiness.UnsupportedCards);
        return $"{ErrorResults.SimulationDeckNotReady} ({role}) Issues: {issues}. Unsupported cards: {unsupported}.";
    }

    private static IReadOnlyCollection<long> BuildDeckTestSeeds(
        IReadOnlyCollection<long>? requestedSeeds,
        int? requestedRunCount
    )
    {
        var uniqueSeeds = requestedSeeds?
            .Where(x => x > 0)
            .Distinct()
            .OrderBy(x => x)
            .ToList();

        if (uniqueSeeds is { Count: > 0 })
        {
            return uniqueSeeds;
        }

        var runCount = Math.Clamp(requestedRunCount.GetValueOrDefault(10), 1, MaxDeckTests);
        var baseSeed = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return Enumerable.Range(0, runCount).Select(i => baseSeed + i).ToList();
    }

    private void ApplySessionSnapshot(RiftboundSimulationRun run, GameSession session)
    {
        run.SnapshotJson = JsonSerializer.Serialize(session, JsonOptions);
        run.ScoreSummaryJson = JsonSerializer.Serialize(
            session
                .Players.Select(p => new
                {
                    p.PlayerIndex,
                    p.DeckId,
                    p.Policy,
                    p.Score,
                })
                .OrderBy(x => x.PlayerIndex)
                .ToList(),
            JsonOptions
        );

        if (session.Phase == RiftboundTurnPhase.Completed)
        {
            run.Status = "completed";
            var maxScore = session.Players.Max(x => x.Score);
            var winners = session.Players.Where(x => x.Score == maxScore).ToList();
            run.WinnerPlayerIndex = winners.Count == 1 ? winners[0].PlayerIndex : null;
        }
        else
        {
            run.Status = "running";
            run.WinnerPlayerIndex = null;
        }
    }

    private static GameSession? DeserializeSession(RiftboundSimulationRun run)
    {
        try
        {
            return JsonSerializer.Deserialize<GameSession>(run.SnapshotJson, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private static RiftboundSimulationDto ToSimulationDto(
        RiftboundSimulationRun run,
        GameSession session,
        IReadOnlyCollection<RiftboundLegalAction> legalActions,
        IReadOnlyCollection<RiftboundSimulationEvent> events
    )
    {
        var scores = session
            .Players.OrderBy(p => p.PlayerIndex)
            .Select(p =>
                new RiftboundSimulationPlayerScoreDto(
                    p.PlayerIndex,
                    p.DeckId,
                    p.Policy,
                    p.Score
                )
            )
            .ToList();

        var battlefields = session
            .Battlefields.OrderBy(b => b.Index)
            .Select(b =>
                new RiftboundSimulationBattlefieldDto(
                    b.Index,
                    b.CardId,
                    b.Name,
                    b.ControlledByPlayerIndex,
                    b.ContestedByPlayerIndex,
                    b.Units.Count(u => u.ControllerPlayerIndex == 0),
                    b.Units.Count(u => u.ControllerPlayerIndex == 1)
                )
            )
            .ToList();

        var eventDtos = events
            .OrderBy(e => e.Sequence)
            .Select(e => new RiftboundSimulationEventDto(e.Sequence, e.EventType, e.PayloadJson, e.CreatedOn))
            .ToList();

        return new RiftboundSimulationDto(
            run.Id,
            run.Seed,
            run.RulesetVersion,
            run.Mode,
            run.Status,
            session.TurnNumber,
            session.TurnPlayerIndex,
            session.Phase,
            session.State,
            run.WinnerPlayerIndex,
            scores,
            battlefields,
            legalActions,
            eventDtos
        );
    }

    private sealed record SimulationLoadResult(
        bool Succeeded,
        ResultStatus Status,
        string? Error,
        RiftboundSimulationRun? Run,
        GameSession? Session,
        List<RiftboundSimulationEvent>? Events
    )
    {
        public static SimulationLoadResult Success(
            RiftboundSimulationRun run,
            GameSession session,
            List<RiftboundSimulationEvent> events
        ) => new(true, ResultStatus.Ok, null, run, session, events);

        public static SimulationLoadResult NotFound() =>
            new(false, ResultStatus.NotFound, ErrorResults.SimulationNotFound, null, null, null);

        public static SimulationLoadResult Failure(string error, ResultStatus status) =>
            new(false, status, error, null, null, null);
    }
}
