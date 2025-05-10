using Infrastructure;

namespace Unit.Tests.RepositoryTests.Base;

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

/// <summary>
///     Base class for mysql database testing that extends the generic test setup <br />
///     Registers DbContext and setup InboxEvent and OutboxEvent to use core db context as base
///     <para />
///     Inherits from <see cref="GenericTestBase{TApplication, TApi, TDbContext}" />. <br />
///     <inheritdoc cref="GenericTestBase{TApplication, TApi, TDbContext}" />
/// </summary>
/// <typeparam name="TApplication">
///     The specific implementation of the core application to test.
/// </typeparam>
/// <typeparam name="TApi">
///     The interface of the core application to test.
/// </typeparam>
/// <typeparam name="TDbContext">
///     The type of the core database context used in tests.
/// </typeparam>
public abstract class PostgreSqlTestBase<TApplication, TApi, TDbContext>
    : GenericTestBase<TApplication, TApi, TDbContext>
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
