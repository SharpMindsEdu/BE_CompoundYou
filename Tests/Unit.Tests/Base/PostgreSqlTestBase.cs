using Infrastructure;
using Unit.Tests.RepositoryTests.Base;

namespace Unit.Tests.Base;

/// <summary>
///     Base class for mysql database testing. <br />
///     Registers DbContext and setup InboxEvent and OutboxEvent to use core db context as base.
///     <para />
///     Inherits from <see cref="GenericTestBase{TDbContext}" />. <br />
///     <inheritdoc cref="GenericTestBase{TDbContext}" />
/// </summary>
/// <typeparam name="TDbContext">
///     The type of the core database context used in tests.
///     <example>
///         <see cref="UserTestDbContext" />
///     </example>
/// </typeparam>
public abstract class PostgreSqlTestBase<TDbContext> : GenericTestBase<TDbContext>
    where TDbContext : DbBaseContext
{
    protected PostgreSqlTestBase(
        PostgreSqlRepositoryTestDatabaseFixture fixture,
        ITestOutputHelper outputHelper,
        string? prefix = "T",
        Guid? dbId = null
    )
        : base(outputHelper)
    {
        Services.RegisterTestDbContext<TDbContext>(fixture, prefix, dbId);
    }
}
