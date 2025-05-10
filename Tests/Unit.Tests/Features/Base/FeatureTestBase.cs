using Infrastructure;
using Unit.Tests.Base;

namespace Unit.Tests.Features.Base;

public class FeatureTestBase(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper,
    string? prefix = "T",
    Guid? dbId = null)
    : PostgreSqlTestBase<ApplicationDbContext>(fixture, outputHelper, prefix, dbId);