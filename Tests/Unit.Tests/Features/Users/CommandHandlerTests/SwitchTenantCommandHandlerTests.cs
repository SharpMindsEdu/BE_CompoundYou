using System.IdentityModel.Tokens.Jwt;
using Application.Features.Users.Commands;
using Application.Shared;
using Domain.Entities;
using Domain.Enums;
using Unit.Tests.Features.Base;

namespace Unit.Tests.Features.Users.CommandHandlerTests;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.UserTests)]
public sealed class SwitchTenantCommandHandlerTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper
) : TenantFeatureTestBase(fixture, outputHelper)
{
    [Fact]
    public async Task SwitchTenant_WithActiveMembership_ShouldIssueTenantBoundToken()
    {
        var user = SeedUser();
        var tenant = SeedTenant("acme", "Acme");
        TenantMembership membership = null!;
        PersistWithDatabase(db =>
        {
            membership = new TenantMembership
            {
                TenantId = tenant.Id,
                UserId = user.Id,
                Role = TenantRole.Manager,
                IsActive = true,
            };
            db.Add(membership);
        });

        var result = await Send(
            new SwitchTenant.SwitchTenantCommand(user.Id, tenant.Id),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(result.Data!.Token);
        Assert.Contains(jwt.Claims, c =>
            c.Type == CompoundYouClaimTypes.TenantId && c.Value == tenant.Id.ToString()
        );
        Assert.Contains(jwt.Claims, c =>
            c.Type == CompoundYouClaimTypes.MembershipId && c.Value == membership.Id.ToString()
        );
        Assert.Contains(jwt.Claims, c =>
            c.Type == CompoundYouClaimTypes.TenantRole && c.Value == TenantRole.Manager.ToString()
        );
    }

    [Fact]
    public async Task SwitchTenant_WithSuspendedTenant_ShouldReturnForbidden()
    {
        var user = SeedUser();
        var tenant = SeedTenant("acme", "Acme");
        tenant.Status = TenantStatus.Suspended;
        PersistWithDatabase(db => db.Update(tenant));
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

        var result = await Send(
            new SwitchTenant.SwitchTenantCommand(user.Id, tenant.Id),
            TestContext.Current.CancellationToken
        );

        Assert.False(result.Succeeded);
        Assert.Equal(ResultStatus.Forbidden, result.Status);
        Assert.Equal(TenancyErrors.TenantSuspended, result.ErrorMessage);
    }
}
