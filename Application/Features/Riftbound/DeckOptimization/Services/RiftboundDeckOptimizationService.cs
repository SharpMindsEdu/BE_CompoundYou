using Application.Features.Riftbound.DeckOptimization.DTOs;
using Application.Features.Riftbound.Decks.Commands;
using Application.Features.Riftbound.Simulation.Engine;
using Application.Features.Riftbound.Simulation.Policies;
using Application.Features.Riftbound.Simulation.Services;
using Application.Shared;
using Domain.Entities.Riftbound;
using Domain.Repositories;
using Domain.Specifications.Riftbound.Decks;
using Domain.Simulation;
using Microsoft.Extensions.Logging;

namespace Application.Features.Riftbound.DeckOptimization.Services;

public sealed class RiftboundDeckOptimizationService(
    IRepository<RiftboundDeckOptimizationRun> runRepository,
    IRepository<RiftboundDeckOptimizationCandidate> candidateRepository,
    IRepository<RiftboundDeckOptimizationMatchup> matchupRepository,
    IRepository<RiftboundDeck> deckRepository,
    IRepository<RiftboundCard> cardRepository,
    IRiftboundDeckSpecification deckSpecification,
    IRiftboundDeckSimulationReadinessService readinessService,
    IRiftboundSimulationDefinitionRegistry definitionRegistry,
    IRiftboundSimulationEngine simulationEngine,
    IMovePolicyResolver movePolicyResolver,
    IRiftboundDeckOptimizationRunQueue runQueue,
    ILogger<RiftboundDeckOptimizationService> logger
) : IRiftboundDeckOptimizationService, IRiftboundDeckOptimizationRunExecutor
{
    private const int DefaultPopulationSize = 40;
    private const int DefaultGenerations = 2;
    private const int DefaultSeedsPerMatch = 5;
    private const int DefaultMaxAutoplaySteps = 1_200;

    public async Task<Result<RiftboundDeckOptimizationRunDto>> CreateRunAsync(
        long userId,
        RiftboundDeckOptimizationRunRequest request,
        CancellationToken cancellationToken
    )
    {
        var normalized = Normalize(request);
        var run = new RiftboundDeckOptimizationRun
        {
            RequestedByUserId = userId,
            Status = "queued",
            Seed = normalized.Seed,
            PopulationSize = normalized.PopulationSize,
            Generations = normalized.Generations,
            SeedsPerMatch = normalized.SeedsPerMatch,
            MaxAutoplaySteps = normalized.MaxAutoplaySteps,
            CurrentGeneration = 0,
            ProgressPercent = 0m,
        };

        await runRepository.Add(run);
        await runRepository.SaveChanges(cancellationToken);
        await runQueue.QueueAsync(run.Id, cancellationToken);

        return Result<RiftboundDeckOptimizationRunDto>.Success(ToRunDto(run, 0, 0));
    }

    public async Task<Result<RiftboundDeckOptimizationRunDto>> GetRunAsync(
        long userId,
        long runId,
        CancellationToken cancellationToken
    )
    {
        var run = await runRepository.GetById(runId);
        if (run is null || run.RequestedByUserId != userId)
        {
            return Result<RiftboundDeckOptimizationRunDto>.Failure(
                ErrorResults.OptimizationRunNotFound,
                ResultStatus.NotFound
            );
        }

        var candidateCount = await candidateRepository.Count(
            x => x.RunId == runId,
            cancellationToken: cancellationToken
        );
        var matchupCount = await matchupRepository.Count(
            x => x.RunId == runId,
            cancellationToken: cancellationToken
        );

        return Result<RiftboundDeckOptimizationRunDto>.Success(
            ToRunDto(run, candidateCount, matchupCount)
        );
    }

    public async Task<Result<RiftboundDeckOptimizationLeaderboardDto>> GetLeaderboardAsync(
        long userId,
        long runId,
        CancellationToken cancellationToken
    )
    {
        var run = await runRepository.GetById(runId);
        if (run is null || run.RequestedByUserId != userId)
        {
            return Result<RiftboundDeckOptimizationLeaderboardDto>.Failure(
                ErrorResults.OptimizationRunNotFound,
                ResultStatus.NotFound
            );
        }

        var generation = run.CurrentGeneration;
        var candidates = await candidateRepository.ListAll(
            x => x.RunId == runId && x.Generation == generation,
            cancellationToken
        );
        var deckIds = candidates.Select(c => c.DeckId).Distinct().ToList();
        var legendIds = candidates.Select(c => c.LegendId).Distinct().ToList();
        var decks = deckIds.Count == 0
            ? []
            : await deckRepository.ListAll(d => deckIds.Contains(d.Id), cancellationToken);
        var legends = legendIds.Count == 0
            ? []
            : await cardRepository.ListAll(c => legendIds.Contains(c.Id), cancellationToken);
        var deckById = decks.ToDictionary(d => d.Id);
        var legendById = legends.ToDictionary(l => l.Id);

        var global = candidates
            .OrderBy(c => c.RankGlobal)
            .ThenBy(c => c.DeckId)
            .Select(c =>
                new RiftboundDeckOptimizationLeaderboardEntryDto(
                    c.RankGlobal,
                    c.RankInLegend,
                    c.Generation,
                    c.DeckId,
                    deckById.TryGetValue(c.DeckId, out var deck) ? deck.Name : $"Deck {c.DeckId}",
                    c.LegendId,
                    legendById.TryGetValue(c.LegendId, out var legend)
                        ? legend.Name
                        : $"Legend {c.LegendId}",
                    c.Wins,
                    c.Losses,
                    c.Draws,
                    c.GamesPlayed,
                    c.WinRate,
                    c.SonnebornBerger,
                    c.HeadToHeadScore
                )
            )
            .ToList();

        var byLegend = global
            .GroupBy(x => new { x.LegendId, x.LegendName })
            .OrderBy(g => g.Key.LegendName, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
                new RiftboundDeckOptimizationLegendLeaderboardDto(
                    g.Key.LegendId,
                    g.Key.LegendName,
                    g.OrderBy(x => x.RankInLegend).ThenBy(x => x.DeckId).ToList()
                )
            )
            .ToList();

        return Result<RiftboundDeckOptimizationLeaderboardDto>.Success(
            new RiftboundDeckOptimizationLeaderboardDto(
                run.Id,
                run.Status,
                generation,
                global,
                byLegend
            )
        );
    }

    public async Task ExecuteRunAsync(long runId, CancellationToken cancellationToken)
    {
        var run = await runRepository.GetById(runId);
        if (run is null)
        {
            return;
        }

        if (
            string.Equals(run.Status, "completed", StringComparison.OrdinalIgnoreCase)
            || string.Equals(run.Status, "running", StringComparison.OrdinalIgnoreCase)
        )
        {
            return;
        }

        run.Status = "running";
        run.ProgressPercent = 0m;
        run.CurrentGeneration = 0;
        run.ErrorMessage = null;
        run.StartedOn = DateTimeOffset.UtcNow;
        run.CompletedOn = null;
        runRepository.Update(run);
        await runRepository.SaveChanges(cancellationToken);

        try
        {
            var random = new Random(unchecked((int)(run.Seed % int.MaxValue)));
            var policy = movePolicyResolver.Resolve(HeuristicMovePolicy.Id);
            var allCards = await cardRepository.ListAll(cancellationToken: cancellationToken);
            var contexts = BuildLegendContexts(allCards).ToList();

            if (contexts.Count == 0)
            {
                await FailRunAsync(run, "Keine validen Kartenpools für Legenden gefunden.", cancellationToken);
                return;
            }

            var population = await CreatePopulationAsync(
                run,
                contexts,
                random,
                generation: 0,
                preferredLegendId: null,
                cancellationToken
            );
            if (population.Count < 2)
            {
                await FailRunAsync(
                    run,
                    "Es konnten nicht genügend simulation-ready Kandidaten erzeugt werden.",
                    cancellationToken
                );
                return;
            }

            var targetPopulation = population.Count;
            var totalEvaluations = run.Generations + 1;
            var evaluation = await EvaluateGenerationAsync(
                run,
                population,
                generation: 0,
                policy,
                cancellationToken
            );
            await PersistGenerationResultsAsync(run, generation: 0, evaluation, cancellationToken);
            await UpdateProgressAsync(run, generation: 0, totalEvaluations, cancellationToken);

            for (var generation = 1; generation <= run.Generations; generation++)
            {
                var survivorIds = SelectSurvivors(evaluation.Candidates, targetPopulation / 2);
                var nextPopulation = population.Where(d => survivorIds.Contains(d.Id)).ToList();
                while (nextPopulation.Count < targetPopulation)
                {
                    var parent = nextPopulation[random.Next(nextPopulation.Count)];
                    var offspring = await CreatePopulationAsync(
                        run,
                        contexts,
                        random,
                        generation,
                        parent.LegendId,
                        cancellationToken,
                        requestedCount: 1
                    );
                    if (offspring.Count == 0)
                    {
                        break;
                    }

                    nextPopulation.Add(offspring[0]);
                }

                if (nextPopulation.Count < 2)
                {
                    await FailRunAsync(run, $"Generation {generation} konnte nicht aufgebaut werden.", cancellationToken);
                    return;
                }

                population = nextPopulation;
                evaluation = await EvaluateGenerationAsync(
                    run,
                    population,
                    generation,
                    policy,
                    cancellationToken
                );
                await PersistGenerationResultsAsync(run, generation, evaluation, cancellationToken);
                await UpdateProgressAsync(run, generation, totalEvaluations, cancellationToken);
            }

            run.Status = "completed";
            run.ProgressPercent = 100m;
            run.CompletedOn = DateTimeOffset.UtcNow;
            runRepository.Update(run);
            await runRepository.SaveChanges(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Deck optimization run {RunId} failed.", runId);
            await FailRunAsync(run, ex.Message, cancellationToken);
        }
    }

    private async Task<List<RiftboundDeck>> CreatePopulationAsync(
        RiftboundDeckOptimizationRun run,
        IReadOnlyCollection<LegendDeckBuildContext> contexts,
        Random random,
        int generation,
        long? preferredLegendId,
        CancellationToken cancellationToken,
        int? requestedCount = null
    )
    {
        var result = new List<RiftboundDeck>();
        var target = requestedCount.GetValueOrDefault(run.PopulationSize);
        var contextPool = preferredLegendId.HasValue
            ? contexts.Where(c => c.Legend.Id == preferredLegendId.Value).ToList()
            : contexts.ToList();
        if (contextPool.Count == 0)
        {
            contextPool = contexts.ToList();
        }

        var attempts = 0;
        var maxAttempts = target * 80;
        while (result.Count < target && attempts < maxAttempts)
        {
            attempts++;
            var context = contextPool[random.Next(contextPool.Count)];
            var selection = BuildRandomSelection(context, random);
            if (selection is null)
            {
                continue;
            }

            var deck = new RiftboundDeck
            {
                Name = BuildDeckName(context.Legend.Name, generation, attempts),
                OwnerId = run.RequestedByUserId,
                LegendId = selection.LegendId,
                ChampionId = selection.ChampionId,
                IsPublic = false,
                Colors = RiftboundDeckCommandHelper.NormalizeColors(context.Legend.Color),
                Cards = selection.Main
                    .Select(x => new RiftboundDeckCard { CardId = x.CardId, Quantity = x.Quantity })
                    .ToList(),
                SideboardCards = selection.Sideboard
                    .Select(x => new RiftboundDeckSideboardCard
                    {
                        CardId = x.CardId,
                        Quantity = x.Quantity,
                    })
                    .ToList(),
                Runes = selection.Runes
                    .Select(x => new RiftboundDeckRune { CardId = x.CardId, Quantity = x.Quantity })
                    .ToList(),
                Battlefields = selection.Battlefields
                    .Select(x => new RiftboundDeckBattlefield { CardId = x })
                    .ToList(),
            };

            await deckRepository.Add(deck);
            await deckRepository.SaveChanges(cancellationToken);

            var loadedDeck = await deckSpecification
                .Reset()
                .IncludeDetails()
                .AccessibleForUser(run.RequestedByUserId)
                .ByDeckId(deck.Id)
                .FirstOrDefault(cancellationToken);
            if (loadedDeck is null)
            {
                continue;
            }

            var readiness = readinessService.Evaluate(loadedDeck);
            if (!readiness.IsSimulationReady)
            {
                deckRepository.Remove(deck);
                await deckRepository.SaveChanges(cancellationToken);
                continue;
            }

            result.Add(loadedDeck);
        }

        return result;
    }

    private async Task<GenerationEvaluation> EvaluateGenerationAsync(
        RiftboundDeckOptimizationRun run,
        IReadOnlyCollection<RiftboundDeck> population,
        int generation,
        IMovePolicy policy,
        CancellationToken cancellationToken
    )
    {
        var decks = population.OrderBy(x => x.Id).ToList();
        var stats = decks.ToDictionary(deck => deck.Id, deck => new CandidateStat(deck));
        var matchups = new List<RiftboundDeckOptimizationMatchup>();
        var pairIndex = 0;

        for (var i = 0; i < decks.Count; i++)
        {
            for (var j = i + 1; j < decks.Count; j++)
            {
                pairIndex++;
                var a = decks[i];
                var b = decks[j];
                var result = await PlayMatchupAsync(
                    run,
                    generation,
                    pairIndex,
                    a,
                    b,
                    policy,
                    cancellationToken
                );

                stats[a.Id].AddResult(b.Id, result.DeckAWins, result.DeckBWins, result.Draws);
                stats[b.Id].AddResult(a.Id, result.DeckBWins, result.DeckAWins, result.Draws);

                matchups.Add(
                    new RiftboundDeckOptimizationMatchup
                    {
                        RunId = run.Id,
                        Generation = generation,
                        DeckAId = a.Id,
                        DeckBId = b.Id,
                        DeckAWins = result.DeckAWins,
                        DeckBWins = result.DeckBWins,
                        Draws = result.Draws,
                        GamesPlayed = result.Games,
                    }
                );
            }
        }

        foreach (var stat in stats.Values)
        {
            stat.ComputeWinRate();
        }

        foreach (var stat in stats.Values)
        {
            stat.SonnebornBerger = stat.PointsByOpponent.Sum(x => x.Value * stats[x.Key].WinRate);
            stat.HeadToHeadScore = stat.PointsByOpponent
                .Where(x => decimal.Abs(stats[x.Key].WinRate - stat.WinRate) < 0.000001m)
                .Sum(x => x.Value);
        }

        var ranked = stats
            .Values.OrderByDescending(x => x.WinRate)
            .ThenByDescending(x => x.SonnebornBerger)
            .ThenByDescending(x => x.HeadToHeadScore)
            .ThenBy(x => x.Deck.Id)
            .ToList();
        for (var index = 0; index < ranked.Count; index++)
        {
            ranked[index].RankGlobal = index + 1;
        }

        foreach (var legendGroup in ranked.GroupBy(x => x.Deck.LegendId))
        {
            var legendRank = 1;
            foreach (var item in legendGroup)
            {
                item.RankInLegend = legendRank++;
            }
        }

        var candidates = ranked
            .Select(x => new RiftboundDeckOptimizationCandidate
            {
                RunId = run.Id,
                DeckId = x.Deck.Id,
                LegendId = x.Deck.LegendId,
                Generation = generation,
                Wins = x.Wins,
                Losses = x.Losses,
                Draws = x.Draws,
                GamesPlayed = x.GamesPlayed,
                WinRate = decimal.Round(x.WinRate, 6),
                SonnebornBerger = decimal.Round(x.SonnebornBerger, 6),
                HeadToHeadScore = decimal.Round(x.HeadToHeadScore, 6),
                RankGlobal = x.RankGlobal,
                RankInLegend = x.RankInLegend,
            })
            .ToList();

        return new GenerationEvaluation(candidates, matchups);
    }

    private async Task PersistGenerationResultsAsync(
        RiftboundDeckOptimizationRun run,
        int generation,
        GenerationEvaluation evaluation,
        CancellationToken cancellationToken
    )
    {
        await candidateRepository.Remove(x => x.RunId == run.Id && x.Generation == generation, cancellationToken);
        await matchupRepository.Remove(x => x.RunId == run.Id && x.Generation == generation, cancellationToken);
        if (evaluation.Candidates.Count > 0)
        {
            await candidateRepository.Add(evaluation.Candidates.ToArray());
        }

        if (evaluation.Matchups.Count > 0)
        {
            await matchupRepository.Add(evaluation.Matchups.ToArray());
        }

        await runRepository.SaveChanges(cancellationToken);
    }

    private async Task<MatchupResult> PlayMatchupAsync(
        RiftboundDeckOptimizationRun run,
        int generation,
        int pairIndex,
        RiftboundDeck deckA,
        RiftboundDeck deckB,
        IMovePolicy policy,
        CancellationToken cancellationToken
    )
    {
        var winsA = 0;
        var winsB = 0;
        var draws = 0;
        for (var game = 0; game < run.SeedsPerMatch; game++)
        {
            var swapped = game % 2 == 1;
            var challenger = swapped ? deckB : deckA;
            var opponent = swapped ? deckA : deckB;
            var seed = BuildSeed(run.Seed, generation, pairIndex, game);
            var session = simulationEngine.CreateSession(
                new RiftboundSimulationEngineSetup(
                    run.Id,
                    run.RequestedByUserId,
                    seed,
                    definitionRegistry.RulesetVersion,
                    challenger,
                    opponent,
                    HeuristicMovePolicy.Id,
                    HeuristicMovePolicy.Id
                )
            );

            var steps = 0;
            while (session.Phase != RiftboundTurnPhase.Completed && steps < run.MaxAutoplaySteps)
            {
                var legalActions = simulationEngine.GetLegalActions(session);
                if (legalActions.Count == 0)
                {
                    break;
                }

                var activePlayer = legalActions.Select(x => x.PlayerIndex).Distinct().Single();
                var chosen = await policy.ChooseActionIdAsync(
                    new RiftboundMovePolicyContext(session, activePlayer, legalActions),
                    cancellationToken
                );
                if (
                    string.IsNullOrWhiteSpace(chosen)
                    || !legalActions.Any(x => string.Equals(x.ActionId, chosen, StringComparison.Ordinal))
                )
                {
                    chosen = legalActions.First().ActionId;
                }

                var applied = simulationEngine.ApplyAction(session, chosen);
                if (!applied.Succeeded)
                {
                    break;
                }

                steps++;
            }

            var winner = ResolveWinner(session);
            if (winner is null)
            {
                draws++;
                continue;
            }

            var winnerIsA = (!swapped && winner.Value == 0) || (swapped && winner.Value == 1);
            if (winnerIsA)
            {
                winsA++;
            }
            else
            {
                winsB++;
            }
        }

        return new MatchupResult(winsA, winsB, draws, run.SeedsPerMatch);
    }

    private static int? ResolveWinner(GameSession session)
    {
        var maxScore = session.Players.Max(x => x.Score);
        var winners = session.Players.Where(x => x.Score == maxScore).ToList();
        return winners.Count == 1 ? winners[0].PlayerIndex : null;
    }

    private static HashSet<long> SelectSurvivors(
        IReadOnlyCollection<RiftboundDeckOptimizationCandidate> candidates,
        int count
    )
    {
        var ordered = candidates.OrderBy(x => x.RankGlobal).ToList();
        var selected = ordered.Take(Math.Max(2, count)).ToList();
        foreach (var legendGroup in ordered.GroupBy(x => x.LegendId))
        {
            if (selected.Any(x => x.LegendId == legendGroup.Key))
            {
                continue;
            }

            selected.Add(legendGroup.OrderBy(x => x.RankGlobal).First());
        }

        return selected.Select(x => x.DeckId).ToHashSet();
    }

    private IEnumerable<LegendDeckBuildContext> BuildLegendContexts(
        IReadOnlyCollection<RiftboundCard> allCards
    )
    {
        var supported = allCards.Where(definitionRegistry.IsCardSupported).ToList();
        var legends = supported.Where(RiftboundDeckCommandHelper.IsLegend).ToList();
        foreach (var legend in legends)
        {
            var colors = RiftboundDeckCommandHelper.NormalizeColors(legend.Color);
            var champions = supported
                .Where(card => RiftboundDeckCommandHelper.ChampionMatchesLegend(card, legend))
                .ToList();
            var mains = supported
                .Where(card =>
                    RiftboundDeckCommandHelper.IsMainDeckCard(card)
                    && RiftboundDeckCommandHelper.CardMatchesColors(card, colors)
                )
                .ToList();
            var runes = supported
                .Where(card =>
                    RiftboundDeckCommandHelper.IsRune(card)
                    && RiftboundDeckCommandHelper.CardMatchesColors(card, colors)
                )
                .ToList();
            var battlefields = supported
                .Where(card =>
                    RiftboundDeckCommandHelper.IsBattlefield(card)
                    && RiftboundDeckCommandHelper.CardMatchesColors(card, colors)
                )
                .DistinctBy(card => card.Id)
                .ToList();

            if (champions.Count == 0 || runes.Count == 0 || battlefields.Count < 3)
            {
                continue;
            }

            if (
                mains.Count * RiftboundDeckCommandHelper.MainAndSideboardCopyLimit
                < RiftboundDeckCommandHelper.MainDeckCardCount
                    + RiftboundDeckCommandHelper.SideboardCardCount
            )
            {
                continue;
            }

            yield return new LegendDeckBuildContext(legend, champions, mains, runes, battlefields);
        }
    }

    private static DeckSelection? BuildRandomSelection(LegendDeckBuildContext context, Random random)
    {
        var shared = new Dictionary<long, int>();
        var main = FillWithCap(
            context.Mains,
            RiftboundDeckCommandHelper.MainDeckCardCount,
            RiftboundDeckCommandHelper.MainAndSideboardCopyLimit,
            random,
            shared
        );
        var sideboard = FillWithCap(
            context.Mains,
            RiftboundDeckCommandHelper.SideboardCardCount,
            RiftboundDeckCommandHelper.MainAndSideboardCopyLimit,
            random,
            shared
        );
        var runes = FillWithCap(
            context.Runes,
            RiftboundDeckCommandHelper.RuneDeckCardCount,
            RiftboundDeckCommandHelper.RuneDeckCardCount,
            random,
            null
        );
        var battlefields = context.Battlefields
            .OrderBy(_ => random.Next())
            .Take(RiftboundDeckCommandHelper.BattlefieldCardCount)
            .Select(x => x.Id)
            .ToList();
        if (main.Count == 0 || sideboard.Count == 0 || runes.Count == 0 || battlefields.Count != 3)
        {
            return null;
        }

        return new DeckSelection(
            context.Legend.Id,
            context.Champions.ElementAt(random.Next(context.Champions.Count)).Id,
            main.Select(x => new RiftboundDeckCardInput(x.Key, x.Value)).ToList(),
            sideboard.Select(x => new RiftboundDeckSideboardCardInput(x.Key, x.Value)).ToList(),
            runes.Select(x => new RiftboundDeckRuneInput(x.Key, x.Value)).ToList(),
            battlefields
        );
    }

    private static Dictionary<long, int> FillWithCap(
        IReadOnlyCollection<RiftboundCard> pool,
        int total,
        int cap,
        Random random,
        Dictionary<long, int>? sharedState
    )
    {
        var result = new Dictionary<long, int>();
        var poolIds = pool.Select(x => x.Id).Distinct().ToList();
        if (poolIds.Count == 0)
        {
            return [];
        }

        var attempts = 0;
        var maxAttempts = total * 160;
        while (result.Values.Sum() < total && attempts < maxAttempts)
        {
            attempts++;
            var id = poolIds[random.Next(poolIds.Count)];
            var sharedCopies = sharedState?.GetValueOrDefault(id) ?? 0;
            var localCopies = result.GetValueOrDefault(id);
            if (Math.Max(sharedCopies, localCopies) >= cap)
            {
                continue;
            }

            result[id] = localCopies + 1;
            if (sharedState is not null)
            {
                sharedState[id] = sharedCopies + 1;
            }
        }

        return result.Values.Sum() == total ? result : [];
    }

    private async Task UpdateProgressAsync(
        RiftboundDeckOptimizationRun run,
        int generation,
        int totalEvaluations,
        CancellationToken cancellationToken
    )
    {
        run.CurrentGeneration = generation;
        run.ProgressPercent = decimal.Round(((generation + 1m) / totalEvaluations) * 100m, 2);
        runRepository.Update(run);
        await runRepository.SaveChanges(cancellationToken);
    }

    private async Task FailRunAsync(
        RiftboundDeckOptimizationRun run,
        string message,
        CancellationToken cancellationToken
    )
    {
        run.Status = "failed";
        run.ProgressPercent = 100m;
        run.ErrorMessage = message;
        run.CompletedOn = DateTimeOffset.UtcNow;
        runRepository.Update(run);
        await runRepository.SaveChanges(cancellationToken);
    }

    private static long BuildSeed(long baseSeed, int generation, int pairIndex, int gameIndex)
    {
        unchecked
        {
            var value = baseSeed;
            value = value * 31 + generation;
            value = value * 31 + pairIndex;
            value = value * 31 + gameIndex;
            return value <= 0 ? Math.Abs(value) + 1 : value;
        }
    }

    private static string BuildDeckName(string legendName, int generation, int attempt)
    {
        var safe = string.IsNullOrWhiteSpace(legendName) ? "Legend" : legendName.Trim();
        var name = $"AI {safe} G{generation} #{attempt}";
        return name.Length <= 150 ? name : name[..150];
    }

    private static RiftboundDeckOptimizationRunDto ToRunDto(
        RiftboundDeckOptimizationRun run,
        int candidateCount,
        int matchupCount
    )
    {
        return new RiftboundDeckOptimizationRunDto(
            run.Id,
            run.Status,
            run.PopulationSize,
            run.Generations,
            run.SeedsPerMatch,
            run.MaxAutoplaySteps,
            run.CurrentGeneration,
            run.ProgressPercent,
            run.Seed,
            run.ErrorMessage,
            run.StartedOn,
            run.CompletedOn,
            candidateCount,
            matchupCount
        );
    }

    private static NormalizedSettings Normalize(RiftboundDeckOptimizationRunRequest request)
    {
        var seed = request.Seed.GetValueOrDefault();
        if (seed <= 0)
        {
            seed = Random.Shared.NextInt64(1, long.MaxValue);
        }

        return new NormalizedSettings(
            PopulationSize: Math.Clamp(request.PopulationSize ?? DefaultPopulationSize, 8, 120),
            Generations: Math.Clamp(request.Generations ?? DefaultGenerations, 0, 8),
            SeedsPerMatch: Math.Clamp(request.SeedsPerMatch ?? DefaultSeedsPerMatch, 1, 20),
            MaxAutoplaySteps: Math.Clamp(request.MaxAutoplaySteps ?? DefaultMaxAutoplaySteps, 50, 2000),
            Seed: seed
        );
    }

    private sealed record NormalizedSettings(
        int PopulationSize,
        int Generations,
        int SeedsPerMatch,
        int MaxAutoplaySteps,
        long Seed
    );

    private sealed record LegendDeckBuildContext(
        RiftboundCard Legend,
        IReadOnlyCollection<RiftboundCard> Champions,
        IReadOnlyCollection<RiftboundCard> Mains,
        IReadOnlyCollection<RiftboundCard> Runes,
        IReadOnlyCollection<RiftboundCard> Battlefields
    );

    private sealed record DeckSelection(
        long LegendId,
        long ChampionId,
        IReadOnlyCollection<RiftboundDeckCardInput> Main,
        IReadOnlyCollection<RiftboundDeckSideboardCardInput> Sideboard,
        IReadOnlyCollection<RiftboundDeckRuneInput> Runes,
        IReadOnlyCollection<long> Battlefields
    );

    private sealed record MatchupResult(int DeckAWins, int DeckBWins, int Draws, int Games);

    private sealed record GenerationEvaluation(
        IReadOnlyCollection<RiftboundDeckOptimizationCandidate> Candidates,
        IReadOnlyCollection<RiftboundDeckOptimizationMatchup> Matchups
    );

    private sealed class CandidateStat(RiftboundDeck deck)
    {
        public RiftboundDeck Deck { get; } = deck;
        public int Wins { get; private set; }
        public int Losses { get; private set; }
        public int Draws { get; private set; }
        public int GamesPlayed { get; private set; }
        public decimal WinRate { get; private set; }
        public decimal SonnebornBerger { get; set; }
        public decimal HeadToHeadScore { get; set; }
        public int RankGlobal { get; set; }
        public int RankInLegend { get; set; }
        public Dictionary<long, decimal> PointsByOpponent { get; } = [];

        public void AddResult(long opponentId, int wins, int losses, int draws)
        {
            Wins += wins;
            Losses += losses;
            Draws += draws;
            GamesPlayed += wins + losses + draws;
            PointsByOpponent[opponentId] = wins + draws * 0.5m;
        }

        public void ComputeWinRate()
        {
            WinRate = GamesPlayed == 0 ? 0m : (Wins + Draws * 0.5m) / GamesPlayed;
        }
    }
}
