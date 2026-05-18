using Application.Features.Tenants.Queries;
using Domain.Entities;
using Domain.Enums;
using Unit.Tests.Features.Base;

namespace Unit.Tests.Features.Tenants.QueryHandlerTests;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.TenantTests)]
public sealed class ListTenantsQueryHandlerTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper
) : TenantFeatureTestBase(fixture, outputHelper)
{
    [Fact]
    public async Task ListTenants_ShouldReturnPagedTenants()
    {
        SeedTenant("acme", "Acme");
        SeedTenant("globex", "Globex");
        SetTenantContext(null, isPlatformAdmin: true);

        var result = await Send(
            new ListTenants.ListTenantsQuery(Page: 1, PageSize: 10),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.Data!.TotalItems);
    }
}

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.TenantTests)]
public sealed class GetMyTenantsQueryHandlerTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper
) : TenantFeatureTestBase(fixture, outputHelper)
{
    [Fact]
    public async Task GetMyTenants_ShouldReturnOnlyActiveMemberships()
    {
        var user = SeedUser();
        var tenantA = SeedTenant("acme", "Acme");
        var tenantB = SeedTenant("globex", "Globex");
        PersistWithDatabase(db =>
            db.AddRange(
                new TenantMembership
                {
                    TenantId = tenantA.Id,
                    UserId = user.Id,
                    Role = TenantRole.TenantAdmin,
                    IsActive = true,
                },
                new TenantMembership
                {
                    TenantId = tenantB.Id,
                    UserId = user.Id,
                    Role = TenantRole.Employee,
                    IsActive = false,
                }
            )
        );

        var result = await Send(
            new GetMyTenants.GetMyTenantsQuery(user.Id),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded);
        Assert.Single(result.Data!);
        Assert.Equal("acme", result.Data![0].Slug);
    }
}
