using System.IO.Compression;
using Application.Features.Gdpr.Commands;
using Domain.Entities;
using Domain.Enums;
using Unit.Tests.Features.Base;

namespace Unit.Tests.Features.Gdpr.CommandHandlerTests;

public abstract class GdprCommandHandlerTestBase(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper
) : TenantFeatureTestBase(fixture, outputHelper)
{
    protected User SeedGdprUserWithRelatedRows()
    {
        var tenant = SeedTenant();
        var user = SeedUser("GDPR User", "gdpr@example.com");
        SetTenantContext(tenant.Id, user.Id);
        PersistWithDatabase(db =>
            db.AddRange(
                new Employee
                {
                    UserId = user.Id,
                    EmployeeNumber = "E-1",
                    FirstName = "GDPR",
                    LastName = "User",
                    Email = "employee@example.com",
                },
                new AuditLogEntry
                {
                    ActorUserId = user.Id,
                    Action = "user.login",
                    EntityType = nameof(User),
                    EntityId = user.Id,
                }
            )
        );
        PersistWithDatabase(db =>
            db.Add(
                new TenantMembership
                {
                    TenantId = tenant.Id,
                    UserId = user.Id,
                    Role = TenantRole.Employee,
                    IsActive = true,
                }
            )
        );
        return user;
    }
}

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.GdprTests)]
public sealed class RequestDataExportCommandHandlerTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper
) : GdprCommandHandlerTestBase(fixture, outputHelper)
{
    [Fact]
    public async Task RequestDataExport_WithExistingUser_ShouldReturnZipWithExpectedEntries()
    {
        var user = SeedGdprUserWithRelatedRows();

        var result = await Send(
            new RequestDataExport.RequestDataExportCommand(user.Id),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded);
        Assert.EndsWith(".zip", result.Data!.FileName);

        using var archive = new ZipArchive(new MemoryStream(result.Data.ZipBytes), ZipArchiveMode.Read);
        Assert.NotNull(archive.GetEntry("user.json"));
        Assert.NotNull(archive.GetEntry("employees.json"));
        Assert.NotNull(archive.GetEntry("memberships.json"));
        Assert.NotNull(archive.GetEntry("audit-log.json"));
    }
}

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.GdprTests)]
public sealed class RequestErasureCommandHandlerTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper
) : GdprCommandHandlerTestBase(fixture, outputHelper)
{
    [Fact]
    public async Task RequestErasure_WithExistingUser_ShouldAnonymizeUserAndRelatedRows()
    {
        var user = SeedGdprUserWithRelatedRows();

        var result = await Send(
            new RequestErasure.RequestErasureCommand(user.Id),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded);
        WithDatabase(db =>
        {
            var erasedUser = db.Set<User>().Single();
            Assert.Equal($"deleted-user-{user.Id}", erasedUser.DisplayName);
            Assert.Null(erasedUser.Email);
            Assert.Null(erasedUser.PhoneNumber);

            var erasedEmployee = db.Set<Employee>().Single();
            Assert.Equal("Deleted", erasedEmployee.FirstName);
            Assert.False(erasedEmployee.IsActive);

            Assert.False(db.Set<TenantMembership>().Single().IsActive);
            Assert.Null(db.Set<AuditLogEntry>().Single().ActorUserId);
        });
    }
}
