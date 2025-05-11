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
}
