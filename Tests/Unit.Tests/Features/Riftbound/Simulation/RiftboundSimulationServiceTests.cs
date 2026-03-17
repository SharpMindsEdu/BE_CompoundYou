using System.Linq.Expressions;
using System.Text.Json;
using Application.Features.Riftbound.Simulation.Definitions;
using Application.Features.Riftbound.Simulation.DTOs;
using Application.Features.Riftbound.Simulation.Engine;
using Application.Features.Riftbound.Simulation.Policies;
using Application.Features.Riftbound.Simulation.Services;
using Application.Shared;
using Domain.Entities.Riftbound;
using Domain.Repositories;
using Domain.Specifications.Riftbound.Decks;
using Domain.Simulation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

namespace Unit.Tests.Features.Riftbound.Simulation;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.RiftboundTests)]
public class RiftboundSimulationServiceTests
{
    [Fact]
    public async Task GetDeckSimulationSupportAsync_ReturnsNotFound_WhenDeckIsNotAccessible()
    {
        var deck = RiftboundSimulationTestData.BuildDeck(1, "Chaos");
        deck.OwnerId = 99;

        var (sut, _, _) = BuildService(
            decks: [deck],
            readinessService: new ConfigurableReadinessService()
        );

        var result = await sut.GetDeckSimulationSupportAsync(1, deck.Id, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ResultStatus.NotFound, result.Status);
        Assert.Contains(ErrorResults.DeckAccessDenied, result.ErrorMessage);
    }

    [Fact]
    public async Task CreateSimulationAsync_ReturnsBadRequest_WhenChallengerDeckNotReady()
    {
        var challenger = RiftboundSimulationTestData.BuildDeck(10, "Chaos");
        var opponent = RiftboundSimulationTestData.BuildDeck(20, "Order");

        var readiness = new ConfigurableReadinessService(
            new Dictionary<long, RiftboundDeckSimulationReadiness>
            {
                [challenger.Id] = new(
                    IsSimulationReady: false,
                    ValidationIssues: ["Main deck must include at least 40 cards."],
                    UnsupportedCards: []
                ),
            }
        );

        var (sut, runRepo, _) = BuildService([challenger, opponent], readinessService: readiness);
        var request = new RiftboundSimulationCreateRequest(
            challenger.Id,
            opponent.Id,
            123,
            HeuristicMovePolicy.Id,
            HeuristicMovePolicy.Id
        );

        var result = await sut.CreateSimulationAsync(1, request, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ResultStatus.BadRequest, result.Status);
        Assert.Contains("(challenger)", result.ErrorMessage);
        Assert.Empty(runRepo.Items);
    }

    [Fact]
    public async Task CreateSimulationAsync_PersistsRunAndSimulationCreatedEvent()
    {
        var challenger = RiftboundSimulationTestData.BuildDeck(11, "Chaos");
        var opponent = RiftboundSimulationTestData.BuildDeck(22, "Order");
        challenger.OwnerId = 1;
        opponent.OwnerId = 1;

        var (sut, runRepo, eventRepo) = BuildService([challenger, opponent]);
        var request = new RiftboundSimulationCreateRequest(
            challenger.Id,
            opponent.Id,
            4567,
            "unknown-policy",
            HeuristicMovePolicy.Id
        );

        var result = await sut.CreateSimulationAsync(1, request, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Data);
        Assert.Equal("running", result.Data!.Status);
        Assert.NotEmpty(result.Data.LegalActions);

        Assert.Single(runRepo.Items);
        var run = runRepo.Items.Single();
        Assert.Equal(4567, run.Seed);
        Assert.Equal(HeuristicMovePolicy.Id, run.ChallengerPolicy);
        Assert.Equal(HeuristicMovePolicy.Id, run.OpponentPolicy);
        Assert.False(string.IsNullOrWhiteSpace(run.SnapshotJson));

        Assert.Single(eventRepo.Items);
        Assert.Equal("simulation-created", eventRepo.Items.Single().EventType);
        Assert.Equal(1, eventRepo.Items.Single().Sequence);
    }

    [Fact]
    public async Task ApplyActionAsync_ReturnsBadRequest_WhenActionIdIsBlank()
    {
        var challenger = RiftboundSimulationTestData.BuildDeck(30, "Chaos");
        var opponent = RiftboundSimulationTestData.BuildDeck(31, "Order");
        challenger.OwnerId = 1;
        opponent.OwnerId = 1;

        var (sut, _, _) = BuildService([challenger, opponent]);
        var create = await sut.CreateSimulationAsync(
            1,
            new RiftboundSimulationCreateRequest(
                challenger.Id,
                opponent.Id,
                999,
                HeuristicMovePolicy.Id,
                HeuristicMovePolicy.Id
            ),
            CancellationToken.None
        );
        Assert.True(create.Succeeded);

        var result = await sut.ApplyActionAsync(
            1,
            create.Data!.SimulationId,
            "   ",
            CancellationToken.None
        );

        Assert.False(result.Succeeded);
        Assert.Equal(ResultStatus.BadRequest, result.Status);
        Assert.Contains(ErrorResults.SimulationActionNotFound, result.ErrorMessage);
    }

    [Fact]
    public async Task ApplyActionAsync_AddsActionEvent_WhenActionIsLegal()
    {
        var challenger = RiftboundSimulationTestData.BuildDeck(40, "Chaos");
        var opponent = RiftboundSimulationTestData.BuildDeck(41, "Order");
        challenger.OwnerId = 1;
        opponent.OwnerId = 1;

        var (sut, _, eventRepo) = BuildService([challenger, opponent]);
        var create = await sut.CreateSimulationAsync(
            1,
            new RiftboundSimulationCreateRequest(
                challenger.Id,
                opponent.Id,
                1234,
                HeuristicMovePolicy.Id,
                HeuristicMovePolicy.Id
            ),
            CancellationToken.None
        );
        Assert.True(create.Succeeded);

        var actionId = create.Data!.LegalActions.First().ActionId;
        var result = await sut.ApplyActionAsync(
            1,
            create.Data.SimulationId,
            actionId,
            CancellationToken.None
        );

        Assert.True(result.Succeeded);
        Assert.Equal(2, eventRepo.Items.Count);
        Assert.Contains(eventRepo.Items, e => e.EventType == "action-applied" && e.Sequence == 2);
    }

    [Fact]
    public async Task AutoPlayAsync_UsesHeuristicFallback_WhenConfiguredPolicyReturnsIllegalAction()
    {
        var challenger = RiftboundSimulationTestData.BuildDeck(50, "Chaos");
        var opponent = RiftboundSimulationTestData.BuildDeck(51, "Order");
        challenger.OwnerId = 1;
        opponent.OwnerId = 1;

        var heuristic = new HeuristicMovePolicy();
        var badPolicy = new FixedMovePolicy("broken", "illegal-action-id");
        var resolver = new MovePolicyResolver([badPolicy, heuristic], heuristic);
        var (sut, _, eventRepo) = BuildService([challenger, opponent], movePolicyResolver: resolver);

        var create = await sut.CreateSimulationAsync(
            1,
            new RiftboundSimulationCreateRequest(
                challenger.Id,
                opponent.Id,
                2026,
                "broken",
                HeuristicMovePolicy.Id
            ),
            CancellationToken.None
        );
        Assert.True(create.Succeeded);

        var autoplay = await sut.AutoPlayAsync(
            1,
            create.Data!.SimulationId,
            new RiftboundSimulationAutoplayRequest(MaxSteps: 1),
            CancellationToken.None
        );

        Assert.True(autoplay.Succeeded);
        Assert.Contains(eventRepo.Items, e => e.EventType == "autoplay-action");
        Assert.Contains(eventRepo.Items, e => e.EventType == "autoplay-finished");

        var actionEvent = eventRepo.Items.Single(e => e.EventType == "autoplay-action");
        using var document = JsonDocument.Parse(actionEvent.PayloadJson);
        var usedPolicy = document.RootElement.GetProperty("policyId").GetString();
        Assert.Equal(HeuristicMovePolicy.Id, usedPolicy);
    }

    [Fact]
    public async Task AutoPlayAsync_UsesPriorityPlayerFromLegalActions_DuringResponseWindow()
    {
        var challenger = RiftboundSimulationTestData.BuildDeck(
            55,
            "Chaos",
            deck =>
            {
                foreach (var card in deck.Cards)
                {
                    card.Card!.Cost = 0;
                }
            }
        );
        var opponent = RiftboundSimulationTestData.BuildDeck(56, "Order");
        challenger.OwnerId = 1;
        opponent.OwnerId = 1;

        var (sut, _, eventRepo) = BuildService([challenger, opponent]);
        var create = await sut.CreateSimulationAsync(
            1,
            new RiftboundSimulationCreateRequest(
                challenger.Id,
                opponent.Id,
                2027,
                HeuristicMovePolicy.Id,
                HeuristicMovePolicy.Id
            ),
            CancellationToken.None
        );
        Assert.True(create.Succeeded);

        var autoplay = await sut.AutoPlayAsync(
            1,
            create.Data!.SimulationId,
            new RiftboundSimulationAutoplayRequest(MaxSteps: 2),
            CancellationToken.None
        );

        Assert.True(autoplay.Succeeded);
        var actionEvents = eventRepo.Items.Where(e => e.EventType == "autoplay-action").ToList();
        Assert.Equal(2, actionEvents.Count);

        using var first = JsonDocument.Parse(actionEvents[0].PayloadJson);
        using var second = JsonDocument.Parse(actionEvents[1].PayloadJson);
        Assert.Equal(0, first.RootElement.GetProperty("playerIndex").GetInt32());
        Assert.Equal(1, second.RootElement.GetProperty("playerIndex").GetInt32());
        Assert.Equal("pass-focus", second.RootElement.GetProperty("actionId").GetString());
    }

    [Fact]
    public async Task GetSimulationAsync_ReturnsNoLegalActions_WhenRunIsCompleted()
    {
        var challenger = RiftboundSimulationTestData.BuildDeck(60, "Chaos");
        var opponent = RiftboundSimulationTestData.BuildDeck(61, "Order");
        challenger.OwnerId = 1;
        opponent.OwnerId = 1;

        var (sut, runRepo, _) = BuildService([challenger, opponent]);
        var create = await sut.CreateSimulationAsync(
            1,
            new RiftboundSimulationCreateRequest(
                challenger.Id,
                opponent.Id,
                42,
                HeuristicMovePolicy.Id,
                HeuristicMovePolicy.Id
            ),
            CancellationToken.None
        );
        Assert.True(create.Succeeded);

        var run = runRepo.Items.Single(r => r.Id == create.Data!.SimulationId);
        run.Status = "completed";
        runRepo.Update(run);

        var result = await sut.GetSimulationAsync(1, run.Id, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Empty(result.Data!.LegalActions);
    }

    [Fact]
    public async Task RunDeckTestsAsync_UsesDistinctProvidedSeedsAndAggregatesResults()
    {
        var challenger = RiftboundSimulationTestData.BuildDeck(70, "Chaos");
        var opponent = RiftboundSimulationTestData.BuildDeck(71, "Order");
        challenger.OwnerId = 1;
        opponent.OwnerId = 1;

        var (sut, _, _) = BuildService([challenger, opponent]);
        var request = new RiftboundDeckTestsRequest(
            challenger.Id,
            opponent.Id,
            Seeds: [5, 3, 5],
            RunCount: 99,
            ChallengerPolicy: HeuristicMovePolicy.Id,
            OpponentPolicy: HeuristicMovePolicy.Id,
            MaxAutoplaySteps: 1_200
        );

        var result = await sut.RunDeckTestsAsync(1, request, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data!.TotalRuns);
        Assert.Equal([3L, 5L], result.Data.Runs.Select(r => r.Seed).ToArray());
        Assert.Equal(
            result.Data.TotalRuns,
            result.Data.ChallengerWins + result.Data.OpponentWins + result.Data.Draws
        );
    }

    private static (
        RiftboundSimulationService Service,
        InMemoryRepository<RiftboundSimulationRun> RunRepo,
        InMemoryRepository<RiftboundSimulationEvent> EventRepo
    ) BuildService(
        IReadOnlyCollection<RiftboundDeck> decks,
        IRiftboundDeckSimulationReadinessService? readinessService = null,
        IRiftboundSimulationDefinitionRegistry? definitionRegistry = null,
        IRiftboundSimulationEngine? simulationEngine = null,
        IMovePolicyResolver? movePolicyResolver = null
    )
    {
        var runRepo = new InMemoryRepository<RiftboundSimulationRun>();
        var eventRepo = new InMemoryRepository<RiftboundSimulationEvent>();
        var readiness = readinessService ?? new ConfigurableReadinessService();
        var registry = definitionRegistry ?? new TestDefinitionRegistry();
        var engine = simulationEngine ?? new RiftboundSimulationEngine();
        var resolver = movePolicyResolver ?? new MovePolicyResolver(
            [new HeuristicMovePolicy()],
            new HeuristicMovePolicy()
        );
        var deckSpecification = new TestDeckSpecification(decks);

        var service = new RiftboundSimulationService(
            runRepo,
            eventRepo,
            deckSpecification,
            readiness,
            registry,
            engine,
            resolver
        );

        return (service, runRepo, eventRepo);
    }

    private sealed class TestDefinitionRegistry : IRiftboundSimulationDefinitionRegistry
    {
        public string RulesetVersion => "test-ruleset";
        public IReadOnlyCollection<string> SupportedKeywords => ["Action", "Reaction"];
        public IReadOnlyCollection<RiftboundRuleCorrection> RuleCorrections =>
            [new("test-correction", "Correction for tests.")];

        public RiftboundSimulationCardDefinition? FindDefinition(RiftboundCard card) => null;

        public bool IsCardSupported(RiftboundCard card) => true;
    }

    private sealed class ConfigurableReadinessService(
        IReadOnlyDictionary<long, RiftboundDeckSimulationReadiness>? map = null
    ) : IRiftboundDeckSimulationReadinessService
    {
        public RiftboundDeckSimulationReadiness Evaluate(RiftboundDeck deck)
        {
            if (map is not null && map.TryGetValue(deck.Id, out var configured))
            {
                return configured;
            }

            return new RiftboundDeckSimulationReadiness(
                IsSimulationReady: true,
                ValidationIssues: [],
                UnsupportedCards: []
            );
        }
    }

    private sealed class FixedMovePolicy(string id, string actionId) : IMovePolicy
    {
        public string PolicyId => id;

        public Task<string?> ChooseActionIdAsync(
            RiftboundMovePolicyContext context,
            CancellationToken cancellationToken
        )
        {
            return Task.FromResult<string?>(actionId);
        }
    }

    private sealed class TestDeckSpecification(IReadOnlyCollection<RiftboundDeck> decks)
        : IRiftboundDeckSpecification
    {
        private Func<RiftboundDeck, bool> _filter = _ => true;
        private bool _orderDescendingByCreatedOn;

        public Expression<Func<RiftboundDeck, bool>>? Criteria { get; private set; }
        public Expression<Func<RiftboundDeck, object>>? OrderBy { get; private set; }
        public bool? OrderAscending { get; private set; }

        public IRiftboundDeckSpecification Reset()
        {
            _filter = _ => true;
            Criteria = null;
            OrderBy = null;
            OrderAscending = null;
            _orderDescendingByCreatedOn = false;
            return this;
        }

        public IRiftboundDeckSpecification IncludeDetails()
        {
            return this;
        }

        public IRiftboundDeckSpecification AccessibleForUser(long userId)
        {
            AddFilter(deck =>
                deck.OwnerId == userId
                || deck.IsPublic
                || deck.Shares.Any(share => share.UserId == userId)
            );
            return this;
        }

        public IRiftboundDeckSpecification ByDeckId(long deckId)
        {
            AddFilter(deck => deck.Id == deckId);
            return this;
        }

        public IRiftboundDeckSpecification FilterBy(
            IReadOnlyCollection<long>? legendIds,
            IReadOnlyCollection<string>? colors
        )
        {
            if (legendIds is { Count: > 0 })
            {
                AddFilter(deck => legendIds.Contains(deck.LegendId));
            }

            if (colors is { Count: > 0 })
            {
                var normalized = colors.Select(c => c.Trim().ToUpperInvariant()).ToHashSet();
                AddFilter(deck =>
                    deck.Colors.Count > 0
                    && deck.Colors.Any(color => normalized.Contains(color.ToUpperInvariant()))
                );
            }

            return this;
        }

        public IRiftboundDeckSpecification OrderByNewest()
        {
            _orderDescendingByCreatedOn = true;
            return this;
        }

        public Func<IQueryable<RiftboundDeck>, IIncludableQueryable<RiftboundDeck, object>>[] GetIncludes()
        {
            return [];
        }

        public ISpecification<RiftboundDeck> ApplyCriteria(Expression<Func<RiftboundDeck, bool>> criteria)
        {
            Criteria = criteria;
            var compiled = criteria.Compile();
            AddFilter(compiled);
            return this;
        }

        public ISpecification<RiftboundDeck> ApplyOrder(
            bool isAscending,
            Expression<Func<RiftboundDeck, object>>? orderByExpression = null
        )
        {
            OrderAscending = isAscending;
            OrderBy = orderByExpression;
            return this;
        }

        public Task<RiftboundDeck?> FirstOrDefault(CancellationToken cancellationToken = default)
        {
            var query = BuildQuery();
            return Task.FromResult(query.FirstOrDefault());
        }

        public Task<List<RiftboundDeck>> ToList(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(BuildQuery().ToList());
        }

        public Task<Page<RiftboundDeck>> ToPage(
            int page = 1,
            int size = 50,
            CancellationToken cancellationToken = default
        )
        {
            var query = BuildQuery().ToList();
            var totalItems = query.Count;
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)size));
            var items = query.Skip((page - 1) * size).Take(size).ToList();
            return Task.FromResult(
                new Page<RiftboundDeck>(
                    page,
                    Math.Min(totalPages, page + 1),
                    totalPages,
                    size,
                    totalItems,
                    items
                )
            );
        }

        private IEnumerable<RiftboundDeck> BuildQuery()
        {
            var query = decks.Where(_filter);
            if (_orderDescendingByCreatedOn)
            {
                query = query.OrderByDescending(d => d.CreatedOn);
            }

            return query;
        }

        private void AddFilter(Func<RiftboundDeck, bool> filter)
        {
            var previous = _filter;
            _filter = deck => previous(deck) && filter(deck);
        }
    }

    private sealed class InMemoryRepository<TEntity> : IRepository<TEntity>
        where TEntity : class
    {
        private long _nextId = 1;
        private readonly PropertyInfoCache _propertyCache = new();
        public List<TEntity> Items { get; } = [];

        public ValueTask<TEntity?> GetById(object? id)
        {
            var entity = id is null ? null : Items.FirstOrDefault(item => IdEquals(item, id));
            return ValueTask.FromResult(entity);
        }

        public Task<TEntity?> GetByExpression(
            Expression<Func<TEntity, bool>> predicate,
            CancellationToken cancellationToken = default
        )
        {
            return Task.FromResult(Items.AsQueryable().FirstOrDefault(predicate));
        }

        public Task<int> Count(
            Expression<Func<TEntity, bool>>? predicate,
            Expression<Func<TEntity, object>>? selector = null,
            CancellationToken cancellationToken = default
        )
        {
            var query = Items.AsQueryable();
            if (predicate is not null)
            {
                query = query.Where(predicate);
            }

            return Task.FromResult(query.Count());
        }

        public Task<List<TEntity>> ListAll(
            Expression<Func<TEntity, bool>>? predicate = null,
            CancellationToken cancellationToken = default
        )
        {
            var query = Items.AsQueryable();
            if (predicate is not null)
            {
                query = query.Where(predicate);
            }

            return Task.FromResult(query.ToList());
        }

        public Task<List<TResult>> ListAll<TResult>(
            Expression<Func<TEntity, TResult>> selector,
            Expression<Func<TEntity, bool>>? predicate = null,
            CancellationToken cancellationToken = default
        )
        {
            var query = Items.AsQueryable();
            if (predicate is not null)
            {
                query = query.Where(predicate);
            }

            return Task.FromResult(query.Select(selector).ToList());
        }

        public Task<Page<TEntity>> ListAllPaged(
            Expression<Func<TEntity, bool>>? predicate = null,
            int page = 1,
            int pageSize = 50,
            CancellationToken cancellationToken = default
        )
        {
            var query = Items.AsQueryable();
            if (predicate is not null)
            {
                query = query.Where(predicate);
            }

            return Task.FromResult(ToPage(query.ToList(), page, pageSize));
        }

        public Task<Page<TResult>> ListAllPaged<TResult>(
            Expression<Func<TEntity, TResult>> selector,
            Expression<Func<TEntity, bool>>? predicate = null,
            int page = 1,
            int pageSize = 50,
            CancellationToken cancellationToken = default
        )
        {
            var query = Items.AsQueryable();
            if (predicate is not null)
            {
                query = query.Where(predicate);
            }

            return Task.FromResult(ToPage(query.Select(selector).ToList(), page, pageSize));
        }

        public Task<List<TEntity>> QueryBySpecification(
            ISpecification<TEntity> spec,
            CancellationToken cancellationToken = default
        )
        {
            throw new NotSupportedException();
        }

        public Task<List<TResult>> QueryBySpecification<TResult>(
            ISpecification<TEntity> spec,
            Expression<Func<TEntity, TResult>> selector,
            CancellationToken cancellationToken = default
        )
        {
            throw new NotSupportedException();
        }

        public Task<Page<TEntity>> QueryBySpecificationPaged(
            ISpecification<TEntity> spec,
            int page = 1,
            int pageSize = 50,
            CancellationToken cancellationToken = default
        )
        {
            throw new NotSupportedException();
        }

        public Task<Page<TResult>> QueryBySpecificationPaged<TResult>(
            ISpecification<TEntity> spec,
            Expression<Func<TEntity, TResult>> selector,
            int page = 1,
            int pageSize = 50,
            CancellationToken cancellationToken = default
        )
        {
            throw new NotSupportedException();
        }

        public Task Add(params TEntity[] entity)
        {
            foreach (var item in entity)
            {
                EnsureId(item);
                Items.Add(item);
            }

            return Task.CompletedTask;
        }

        public void Update(params TEntity[] entity) { }

        public Task<int> Update(
            Expression<Func<SetPropertyCalls<TEntity>, SetPropertyCalls<TEntity>>> setExpression,
            Expression<Func<TEntity, bool>>? predicate = null,
            CancellationToken cancellationToken = default
        )
        {
            return Task.FromResult(0);
        }

        public void Remove(params TEntity[] entity)
        {
            foreach (var item in entity)
            {
                Items.Remove(item);
            }
        }

        public Task<int> Remove(
            Expression<Func<TEntity, bool>> predicate,
            CancellationToken cancellationToken = default
        )
        {
            var compiled = predicate.Compile();
            var removed = Items.RemoveAll(item => compiled(item));
            return Task.FromResult(removed);
        }

        public Task SaveChanges(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<bool> Exist(Expression<Func<TEntity, bool>> predicate, CancellationToken ct)
        {
            return Task.FromResult(Items.AsQueryable().Any(predicate));
        }

        private void EnsureId(TEntity item)
        {
            var idProperty = _propertyCache.IdProperty ??= typeof(TEntity).GetProperty("Id");
            if (idProperty is null || idProperty.PropertyType != typeof(long))
            {
                return;
            }

            var current = (long)(idProperty.GetValue(item) ?? 0L);
            if (current > 0)
            {
                return;
            }

            idProperty.SetValue(item, _nextId++);
        }

        private bool IdEquals(TEntity item, object rawId)
        {
            var idProperty = _propertyCache.IdProperty ??= typeof(TEntity).GetProperty("Id");
            if (idProperty is null)
            {
                return false;
            }

            var entityId = idProperty.GetValue(item);
            return entityId is not null && entityId.Equals(Convert.ChangeType(rawId, entityId.GetType()));
        }

        private static Page<TValue> ToPage<TValue>(IReadOnlyCollection<TValue> source, int page, int size)
        {
            var totalItems = source.Count;
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)size));
            var items = source.Skip((page - 1) * size).Take(size).ToList();
            return new Page<TValue>(
                page,
                Math.Min(totalPages, page + 1),
                totalPages,
                size,
                totalItems,
                items
            );
        }

        private sealed class PropertyInfoCache
        {
            public System.Reflection.PropertyInfo? IdProperty { get; set; }
        }
    }
}
