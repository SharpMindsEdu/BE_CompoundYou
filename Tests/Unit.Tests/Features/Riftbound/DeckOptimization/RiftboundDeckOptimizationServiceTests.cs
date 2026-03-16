using System.Linq.Expressions;
using Application.Features.Riftbound.DeckOptimization.Services;
using Application.Features.Riftbound.Simulation.Definitions;
using Application.Features.Riftbound.Simulation.Engine;
using Application.Features.Riftbound.Simulation.Policies;
using Application.Features.Riftbound.Simulation.Services;
using Domain.Entities.Riftbound;
using Domain.Repositories;
using Domain.Specifications.Riftbound.Decks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.Logging.Abstractions;

namespace Unit.Tests.Features.Riftbound.DeckOptimization;

[Trait("category", ServiceTestCategories.UnitTests)]
public class RiftboundDeckOptimizationServiceTests
{
    [Fact]
    public async Task CreateExecuteAndReadLeaderboard_WorksEndToEnd()
    {
        var cards = BuildCards();
        var runRepo = new InMemoryRepository<RiftboundDeckOptimizationRun>();
        var candidateRepo = new InMemoryRepository<RiftboundDeckOptimizationCandidate>();
        var matchupRepo = new InMemoryRepository<RiftboundDeckOptimizationMatchup>();
        var deckRepo = new InMemoryRepository<RiftboundDeck>();
        var cardRepo = new InMemoryRepository<RiftboundCard>(cards);
        var deckSpec = new TestDeckSpecification(deckRepo, cardRepo);
        var registry = new SupportAllRegistry();
        var readiness = new RiftboundDeckSimulationReadinessService(registry);
        var queue = new RiftboundDeckOptimizationRunQueue();
        var heuristic = new HeuristicMovePolicy();
        var moveResolver = new MovePolicyResolver([heuristic], heuristic);

        var sut = new RiftboundDeckOptimizationService(
            runRepo,
            candidateRepo,
            matchupRepo,
            deckRepo,
            cardRepo,
            deckSpec,
            readiness,
            registry,
            new RiftboundSimulationEngine(),
            moveResolver,
            queue,
            NullLogger<RiftboundDeckOptimizationService>.Instance
        );

        var create = await sut.CreateRunAsync(
            1,
            new RiftboundDeckOptimizationRunRequest(
                PopulationSize: 8,
                Generations: 1,
                SeedsPerMatch: 1,
                MaxAutoplaySteps: 400,
                Seed: 1337
            ),
            CancellationToken.None
        );

        Assert.True(create.Succeeded);
        await sut.ExecuteRunAsync(create.Data!.RunId, CancellationToken.None);

        var run = await sut.GetRunAsync(1, create.Data.RunId, CancellationToken.None);
        Assert.True(run.Succeeded);
        Assert.Equal("completed", run.Data!.Status);

        var leaderboard = await sut.GetLeaderboardAsync(1, create.Data.RunId, CancellationToken.None);
        Assert.True(leaderboard.Succeeded);
        Assert.NotEmpty(leaderboard.Data!.Global);
        Assert.NotEmpty(leaderboard.Data.ByLegend);
    }

    private static IReadOnlyCollection<RiftboundCard> BuildCards()
    {
        var cards = new List<RiftboundCard>();
        var id = 1L;

        static RiftboundCard Card(long id, string name, string type, string color) =>
            new()
            {
                Id = id,
                ReferenceId = $"ref-{id}",
                Name = name,
                Type = type,
                Color = [color],
                Cost = type is "Unit" or "Spell" ? 1 : null,
                Might = type == "Unit" ? 1 : null,
            };

        foreach (var color in new[] { "Chaos", "Order" })
        {
            cards.Add(Card(id++, $"{color} Legend", "Legend", color));
            cards.Add(Card(id++, $"{color} Champion A", "Champion", color));
            cards.Add(Card(id++, $"{color} Champion B", "Champion", color));

            for (var i = 0; i < 20; i++)
            {
                cards.Add(Card(id++, $"{color} Unit {i:00}", "Unit", color));
            }

            for (var i = 0; i < 6; i++)
            {
                cards.Add(Card(id++, $"{color} Rune {i:00}", "Rune", color));
            }

            for (var i = 0; i < 3; i++)
            {
                cards.Add(Card(id++, $"{color} Battlefield {i:00}", "Battlefield", color));
            }
        }

        return cards;
    }

    private sealed class SupportAllRegistry : IRiftboundSimulationDefinitionRegistry
    {
        public string RulesetVersion => "test-rules";
        public IReadOnlyCollection<string> SupportedKeywords => [];
        public IReadOnlyCollection<RiftboundRuleCorrection> RuleCorrections => [];

        public RiftboundSimulationCardDefinition? FindDefinition(RiftboundCard card) => null;
        public bool IsCardSupported(RiftboundCard card) => true;
    }

    private sealed class TestDeckSpecification(
        InMemoryRepository<RiftboundDeck> deckRepository,
        InMemoryRepository<RiftboundCard> cardRepository
    ) : IRiftboundDeckSpecification
    {
        private Func<RiftboundDeck, bool> _filter = _ => true;
        private bool _includeDetails;

        public Expression<Func<RiftboundDeck, bool>>? Criteria { get; private set; }
        public Expression<Func<RiftboundDeck, object>>? OrderBy { get; private set; }
        public bool? OrderAscending { get; private set; }

        public IRiftboundDeckSpecification Reset()
        {
            _filter = _ => true;
            _includeDetails = false;
            return this;
        }

        public IRiftboundDeckSpecification IncludeDetails()
        {
            _includeDetails = true;
            return this;
        }

        public IRiftboundDeckSpecification AccessibleForUser(long userId)
        {
            AddFilter(d => d.OwnerId == userId || d.IsPublic || d.Shares.Any(s => s.UserId == userId));
            return this;
        }

        public IRiftboundDeckSpecification ByDeckId(long deckId)
        {
            AddFilter(d => d.Id == deckId);
            return this;
        }

        public IRiftboundDeckSpecification FilterBy(
            IReadOnlyCollection<long>? legendIds,
            IReadOnlyCollection<string>? colors
        )
        {
            if (legendIds is { Count: > 0 })
            {
                AddFilter(d => legendIds.Contains(d.LegendId));
            }

            if (colors is { Count: > 0 })
            {
                var normalized = colors.Select(c => c.Trim().ToUpperInvariant()).ToHashSet();
                AddFilter(d => d.Colors.Any(c => normalized.Contains(c.ToUpperInvariant())));
            }

            return this;
        }

        public IRiftboundDeckSpecification OrderByNewest()
        {
            return this;
        }

        public Func<IQueryable<RiftboundDeck>, IIncludableQueryable<RiftboundDeck, object>>[] GetIncludes() =>
            [];

        public ISpecification<RiftboundDeck> ApplyCriteria(Expression<Func<RiftboundDeck, bool>> criteria)
        {
            Criteria = criteria;
            AddFilter(criteria.Compile());
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
            var deck = deckRepository.Items.Where(_filter).FirstOrDefault();
            return Task.FromResult(deck is null ? null : Hydrate(deck));
        }

        public Task<List<RiftboundDeck>> ToList(CancellationToken cancellationToken = default)
        {
            var list = deckRepository.Items.Where(_filter).Select(Hydrate).ToList();
            return Task.FromResult(list);
        }

        public Task<Page<RiftboundDeck>> ToPage(
            int page = 1,
            int size = 50,
            CancellationToken cancellationToken = default
        )
        {
            var list = deckRepository.Items.Where(_filter).Select(Hydrate).ToList();
            var total = list.Count;
            var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)size));
            var items = list.Skip((page - 1) * size).Take(size).ToList();
            return Task.FromResult(
                new Page<RiftboundDeck>(page, Math.Min(totalPages, page + 1), totalPages, size, total, items)
            );
        }

        private RiftboundDeck Hydrate(RiftboundDeck deck)
        {
            if (!_includeDetails)
            {
                return deck;
            }

            var cards = cardRepository.Items.ToDictionary(c => c.Id);
            deck.Legend = cards.GetValueOrDefault(deck.LegendId);
            deck.Champion = cards.GetValueOrDefault(deck.ChampionId);
            foreach (var entry in deck.Cards)
            {
                entry.Card = cards.GetValueOrDefault(entry.CardId);
            }

            foreach (var entry in deck.SideboardCards)
            {
                entry.Card = cards.GetValueOrDefault(entry.CardId);
            }

            foreach (var entry in deck.Runes)
            {
                entry.Card = cards.GetValueOrDefault(entry.CardId);
            }

            foreach (var entry in deck.Battlefields)
            {
                entry.Card = cards.GetValueOrDefault(entry.CardId);
            }

            return deck;
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
        public List<TEntity> Items { get; }

        public InMemoryRepository()
        {
            Items = [];
        }

        public InMemoryRepository(IEnumerable<TEntity> items)
        {
            Items = items.ToList();
            _nextId = Items.Select(GetId).DefaultIfEmpty(0).Max() + 1;
        }

        public ValueTask<TEntity?> GetById(object? id)
        {
            if (id is null)
            {
                return ValueTask.FromResult<TEntity?>(null);
            }

            return ValueTask.FromResult(Items.FirstOrDefault(x => GetId(x) == Convert.ToInt64(id)));
        }

        public Task<TEntity?> GetByExpression(
            Expression<Func<TEntity, bool>> predicate,
            CancellationToken cancellationToken = default
        ) => Task.FromResult(Items.AsQueryable().FirstOrDefault(predicate));

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
        ) => throw new NotSupportedException();

        public Task<Page<TResult>> ListAllPaged<TResult>(
            Expression<Func<TEntity, TResult>> selector,
            Expression<Func<TEntity, bool>>? predicate = null,
            int page = 1,
            int pageSize = 50,
            CancellationToken cancellationToken = default
        ) => throw new NotSupportedException();

        public Task<List<TEntity>> QueryBySpecification(
            ISpecification<TEntity> spec,
            CancellationToken cancellationToken = default
        ) => throw new NotSupportedException();

        public Task<List<TResult>> QueryBySpecification<TResult>(
            ISpecification<TEntity> spec,
            Expression<Func<TEntity, TResult>> selector,
            CancellationToken cancellationToken = default
        ) => throw new NotSupportedException();

        public Task<Page<TEntity>> QueryBySpecificationPaged(
            ISpecification<TEntity> spec,
            int page = 1,
            int pageSize = 50,
            CancellationToken cancellationToken = default
        ) => throw new NotSupportedException();

        public Task<Page<TResult>> QueryBySpecificationPaged<TResult>(
            ISpecification<TEntity> spec,
            Expression<Func<TEntity, TResult>> selector,
            int page = 1,
            int pageSize = 50,
            CancellationToken cancellationToken = default
        ) => throw new NotSupportedException();

        public Task Add(params TEntity[] entity)
        {
            foreach (var item in entity)
            {
                if (GetId(item) == 0)
                {
                    SetId(item, _nextId++);
                }

                Items.Add(item);
            }

            return Task.CompletedTask;
        }

        public void Update(params TEntity[] entity) { }

        public Task<int> Update(
            Expression<Func<SetPropertyCalls<TEntity>, SetPropertyCalls<TEntity>>> setExpression,
            Expression<Func<TEntity, bool>>? predicate = null,
            CancellationToken cancellationToken = default
        ) => Task.FromResult(0);

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
            var removed = Items.RemoveAll(x => predicate.Compile()(x));
            return Task.FromResult(removed);
        }

        public Task SaveChanges(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<bool> Exist(Expression<Func<TEntity, bool>> predicate, CancellationToken ct) =>
            Task.FromResult(Items.AsQueryable().Any(predicate));

        private static long GetId(TEntity entity)
        {
            var property = typeof(TEntity).GetProperty("Id");
            return property is null ? 0 : Convert.ToInt64(property.GetValue(entity) ?? 0L);
        }

        private static void SetId(TEntity entity, long id)
        {
            var property = typeof(TEntity).GetProperty("Id");
            property?.SetValue(entity, id);
        }
    }
}
