using Domain.Enums;
using Application.Features.CareerPaths.Queries;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.CareerPaths.Queries;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.CareerPathTests)]
public sealed class GetMyCareerPathEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task GetMyCareerPath_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Get,
            GetMyCareerPath.Endpoint,
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task GetMyCareerPath_WithSeededData_ReturnsExpectedResult()
    {
        var ct = TestContext.Current.CancellationToken;

        var ctx = await CreateTenantContextAsync(TenantRole.Employee, cancellationToken: ct);
        Assert.NotNull(ctx.Employee);
        var career = await SeedCareerPathDataAsync(ctx.Tenant, ctx.Employee, null, ct);

        var json = await SendAuthorizedJsonAsync(
            HttpMethod.Get,
            WithQuery("api/career-paths/me", ("targetRoleProfileId", career.TargetRole.Id)),
            ctx.Token,
            cancellationToken: ct
        );

        Assert.Equal(ctx.Employee.Id, GetRequiredLong(json, "employeeId"));
    
    }
}
