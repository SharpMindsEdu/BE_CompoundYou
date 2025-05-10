using System.Collections.Concurrent;
using Application.Extensions;
using Infrastructure.Extensions;
using Infrastructure.Repositories;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Unit.Tests.RepositoryTests.Base;

namespace Unit.Tests.Base;

/// <summary>
///     Base class for generic test setup with dependency injection, logging, and database management.
/// </summary>
/// <typeparam name="TDbContext">
///     The type of the core database context used in tests.
///     <example>
///         <see cref="UserTestDbContext" />
///     </example>
/// </typeparam>
public class GenericTestBase<TDbContext> : IAsyncLifetime
    where TDbContext : DbContext
{
    private static readonly ConcurrentBag<string?> Registered = new();
    private ServiceProvider? _serviceProvider;

    /// <summary>
    ///     Initializes a new instance of the <see cref="GenericTestBase{TDbContext}" /> class.
    ///     Configures dependency injection, logging, and repositories.
    /// </summary>
    /// <param name="outputHelper">The test output helper for logging.</param>
    protected GenericTestBase(ITestOutputHelper outputHelper)
    {
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddProvider(new XunitLoggerProvider(outputHelper));
            builder.SetMinimumLevel(LogLevel.Debug);
        });
        
        Services.AddApplicationRegistration();
        Services.AddInfrastructurePipelineBehaviors();
        Services.AddSingleton(loggerFactory);
        Services.AddLogging();
    }

    protected IServiceCollection Services { get; } = new ServiceCollection();
    protected ServiceProvider ServiceProvider
    {
        get => _serviceProvider ??= Services.BuildServiceProvider();
        set => _serviceProvider = value;
    }

    /// <summary>
    ///     Cleans up resources asynchronously.
    /// </summary>
    public virtual async ValueTask DisposeAsync()
    {
        try
        {
            using var scope = ServiceProvider.CreateScope();
            var dbContextFactories = scope.ServiceProvider.GetServices<IDbContextFactory>();
            Task.WaitAll(dbContextFactories.Select(ClearDatabase).ToArray());
        }
        catch (Exception)
        {
            // ignored
        }

        await ServiceProvider.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Initializes the test environment by building the service provider.
    /// </summary>
    public virtual async ValueTask InitializeAsync()
    {
        await BuildServiceProvider();
    }

    /// <summary>
    ///     Builds the service provider and initializes the database.
    /// </summary>
    /// <param name="useMigrate">Indicates whether to apply migrations or just ensure database creation.</param>
    protected virtual async Task BuildServiceProvider(bool useMigrate = true)
    {
        ServiceProvider = Services.BuildServiceProvider();

        using var scope = ServiceProvider.CreateScope();
        var dbContextFactories = scope.ServiceProvider.GetServices<IDbContextFactory>();

        foreach (var factory in dbContextFactories)
        {
            await using var context = factory.GetDbContext();
            try
            {
                var createScript = context.Database.GenerateCreateScript();
                await context.Database.ExecuteSqlRawAsync(createScript);
            }
            catch
            {
            }

            await ClearDatabase(factory);
        }
    }

    /// <summary>
    ///     Clears all data from the database.
    /// </summary>
    /// <param name="contextFactory">The database context factory.</param>
    protected async Task ClearDatabase(IDbContextFactory contextFactory)
    {
        await using var db = contextFactory.GetDbContext();

        lock (db.Database)
        {
            if (db.Database.CanConnect())
            {
                var tableNames = db
                    .Model.GetEntityTypes()
                    .Select(e => new
                    {
                        Schema = e.GetSchema() ?? "public", // default schema fallback
                        Name = e.GetTableName(),
                    })
                    .Where(t => !string.IsNullOrEmpty(t.Name))
                    .Distinct()
                    .Select(t => $"\"{t.Schema}\".\"{t.Name}\"") // wichtig: quotes für case-sensitivity
                    .ToList();

                if (tableNames.Count == 0)
                    return;

                var truncateSql =
                    $"TRUNCATE {string.Join(", ", tableNames)} RESTART IDENTITY CASCADE;";
                db.Database.ExecuteSqlRaw(truncateSql);
            }
        }
    }

    /// <summary>
    ///     Executes an operation using the database context.
    /// </summary>
    /// <param name="operation">The operation to perform on the database context.</param>
    protected virtual void WithDatabase(Action<TDbContext> operation)
    {
        using var scope = ServiceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TDbContext>();

        operation(db);
    }

    /// <summary>
    ///     Seeds the database with data and persists changes.
    /// </summary>
    /// <typeparam name="TADbContext">The type of the database context.</typeparam>
    /// <param name="seeding">The seeding operation.</param>
    public virtual void PersistWithDatabase<TADbContext>(Action<TADbContext> seeding)
        where TADbContext : DbContext
    {
        using var scope = ServiceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TADbContext>();

        seeding(db);

        try
        {
            db.SaveChanges();
        }
        catch (Exception ex)
        {
            Assert.Fail($"{ex.Message}\n\n Inner Exception:\n{ex.InnerException}");
        }
    }

    /// <summary>
    ///     Seeds the database with data using the generic core database context (<see cref="TDbContext" />).
    /// </summary>
    /// <param name="seeding">The seeding operation.</param>
    public virtual void PersistWithDatabase(Action<TDbContext> seeding)
    {
        PersistWithDatabase<TDbContext>(seeding);
    }

    protected virtual async Task<TResult> Send<TResult>(IRequest<TResult> request, CancellationToken cancellationToken = default)
    {
        using var scope = ServiceProvider.CreateScope();
        var mediatr = scope.ServiceProvider.GetRequiredService<IMediator>();
        return await mediatr.Send(request, cancellationToken);
    }
}
