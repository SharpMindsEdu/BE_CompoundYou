using Application.Features.TenantMemberships.Queries;
using Application.Shared;
using Domain.Entities;
using Domain.Enums;
using Unit.Tests.Features.Base;

namespace Unit.Tests.Features.TenantMemberships.QueryHandlerTests;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.TenantMembershipTests)]
public sealed class ListMembersQueryHandlerTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper
) : TenantFeatureTestBase(fixture, outputHelper)
{
    [Fact]
    public async Task ListMembers_ForDifferentTenant_ShouldReturnForbidden()
    {
        var tenant = SeedTenant("tenant-a");
        var otherTenant = SeedTenant("tenant-b");
        SetTenantContext(otherTenant.Id);

        var result = await Send(
            new ListMembers.ListMembersQuery(tenant.Id),
            TestContext.Current.CancellationToken
        );

        Assert.False(result.Succeeded);
        Assert.Equal(ResultStatus.Forbidden, result.Status);
        Assert.Equal(ErrorResults.Forbidden, result.ErrorMessage);
    }

    [Fact]
    public async Task ListMembers_ForCurrentTenant_ShouldReturnPagedMemberships()
    {
        var tenant = SeedTenant();
        var user = SeedUser();
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
        SetTenantContext(tenant.Id);

        var result = await Send(
            new ListMembers.ListMembersQuery(tenant.Id, Page: 1, PageSize: 10),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded);
        Assert.Single(result.Data!.Items);
        Assert.Equal(user.Id, result.Data.Items.Single().UserId);
    }
}
