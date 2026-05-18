using Application.Features.Audit.Queries;
using Domain.Entities;
using Unit.Tests.Features.Base;

namespace Unit.Tests.Features.Audit.QueryHandlerTests;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.AuditTests)]
public sealed class ListAuditEntriesQueryHandlerTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper
) : TenantFeatureTestBase(fixture, outputHelper)
{
    [Fact]
    public async Task ListAuditEntries_WithFilters_ShouldReturnMatchingEntries()
    {
        var tenant = SeedTenant();
        SetTenantContext(tenant.Id);
        PersistWithDatabase(db =>
            db.AddRange(
                new AuditLogEntry
                {
                    ActorUserId = 10,
                    Action = "employee.create",
                    EntityType = nameof(Employee),
                    EntityId = 1,
                    OccurredOn = DateTimeOffset.UtcNow.AddMinutes(-5),
                },
                new AuditLogEntry
                {
                    ActorUserId = 11,
                    Action = "team.create",
                    EntityType = nameof(Team),
                    EntityId = 2,
                    OccurredOn = DateTimeOffset.UtcNow,
                }
            )
        );

        var result = await Send(
            new ListAuditEntries.ListAuditEntriesQuery(
                nameof(Employee),
                1,
                10,
                DateTimeOffset.UtcNow.AddHours(-1),
                DateTimeOffset.UtcNow.AddHours(1)
            ),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded);
        var entry = Assert.Single(result.Data!.Items);
        Assert.Equal("employee.create", entry.Action);
    }
}
