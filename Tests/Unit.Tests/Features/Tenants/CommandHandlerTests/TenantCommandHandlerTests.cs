using Application.Features.Tenants.Commands;
using Application.Shared;
using Domain.Enums;
using Unit.Tests.Features.Base;

namespace Unit.Tests.Features.Tenants.CommandHandlerTests;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.TenantTests)]
public sealed class CreateTenantCommandHandlerTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper
) : TenantFeatureTestBase(fixture, outputHelper)
{
    [Fact]
    public async Task CreateTenant_WithUniqueSlug_ShouldCreateActiveTenant()
    {
        SetTenantContext(null, isPlatformAdmin: true);
        var owner = SeedUser(email: "owner@example.com");

        var result = await Send(
            new CreateTenant.CreateTenantCommand("Acme", "acme", "pro", owner.Id),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded);
        Assert.Equal(TenantStatus.Active, result.Data!.Status);
        Assert.Equal(owner.Id, result.Data.OwnerUserId);
    }

    [Fact]
    public async Task CreateTenant_WithDuplicateSlug_ShouldReturnConflict()
    {
        SeedTenant("acme", "Acme");
        SetTenantContext(null, isPlatformAdmin: true);

        var result = await Send(
            new CreateTenant.CreateTenantCommand("Other", "acme", null, null),
            TestContext.Current.CancellationToken
        );

        Assert.False(result.Succeeded);
        Assert.Equal(ResultStatus.Conflict, result.Status);
        Assert.Equal(TenancyErrors.SlugAlreadyTaken, result.ErrorMessage);
    }
}

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.TenantTests)]
public sealed class UpdateTenantCommandHandlerTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper
) : TenantFeatureTestBase(fixture, outputHelper)
{
    [Fact]
    public async Task UpdateTenant_AsDifferentTenantAdmin_ShouldReturnForbidden()
    {
        var tenant = SeedTenant("acme", "Acme");
        var otherTenant = SeedTenant("other", "Other");
        SetTenantContext(otherTenant.Id);

        var result = await Send(
            new UpdateTenant.UpdateTenantCommand(tenant.Id, "Acme Updated", "enterprise"),
            TestContext.Current.CancellationToken
        );

        Assert.False(result.Succeeded);
        Assert.Equal(ResultStatus.Forbidden, result.Status);
        Assert.Equal(ErrorResults.Forbidden, result.ErrorMessage);
    }

    [Fact]
    public async Task UpdateTenant_AsSameTenantAdmin_ShouldUpdateNameAndPlan()
    {
        var tenant = SeedTenant("acme", "Acme");
        SetTenantContext(tenant.Id);

        var result = await Send(
            new UpdateTenant.UpdateTenantCommand(tenant.Id, "Acme Updated", "enterprise"),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded);
        Assert.Equal("Acme Updated", result.Data!.Name);
        Assert.Equal("enterprise", result.Data.Plan);
    }
}

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.TenantTests)]
public sealed class SuspendTenantCommandHandlerTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper
) : TenantFeatureTestBase(fixture, outputHelper)
{
    [Fact]
    public async Task SuspendTenant_ShouldToggleTenantStatus()
    {
        var tenant = SeedTenant("acme", "Acme");
        SetTenantContext(null, isPlatformAdmin: true);

        var result = await Send(
            new SuspendTenant.SuspendTenantCommand(tenant.Id, true),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded);
        Assert.Equal(TenantStatus.Suspended, result.Data!.Status);
    }
}
