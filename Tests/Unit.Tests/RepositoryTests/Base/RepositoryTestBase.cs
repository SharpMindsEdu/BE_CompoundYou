using Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Unit.Tests.Base;

namespace Unit.Tests.RepositoryTests.Base;

public class RepositoryTestBase : PostgreSqlTestBase<UserTestDbContext>
{
    public RepositoryTestBase(
        PostgreSqlRepositoryTestDatabaseFixture fixture,
        ITestOutputHelper outputHelper,
        string? prefix = "T",
        Guid? dbId = null
    )
        : base(fixture, outputHelper, prefix, dbId)
    {
        Services.RegisterTestDbContext<EmployeeTestDbContext>(fixture, prefix, dbId);
    }

    protected override async Task BuildServiceProvider(bool useMigrate = true)
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
            catch { }

            await ClearDatabase(factory);
        }
    }
}
