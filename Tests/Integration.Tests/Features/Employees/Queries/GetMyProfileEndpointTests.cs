using Domain.Enums;
using Application.Features.Employees.Queries;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.Employees.Queries;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.EmployeeTests)]
public sealed class GetMyProfileEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task GetMyProfile_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Get,
            GetMyProfile.Endpoint,
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task GetMyProfile_WithSeededData_ReturnsExpectedResult()
    {
        var ct = TestContext.Current.CancellationToken;

        var ctx = await CreateTenantContextAsync(TenantRole.Employee, cancellationToken: ct);
        Assert.NotNull(ctx.Employee);

        var json = await SendAuthorizedJsonAsync(
            HttpMethod.Get,
            "api/employees/me",
            ctx.Token,
            cancellationToken: ct
        );

        Assert.Equal(ctx.Employee.Id, GetRequiredLong(json, "id"));
    
    }
}
