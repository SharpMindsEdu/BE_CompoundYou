using Domain.Enums;
using Application.Features.Employees.Commands;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.Employees.Commands;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.EmployeeTests)]
public sealed class CreateEmployeeEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task CreateEmployee_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Post,
            CreateEmployee.Endpoint,
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task CreateEmployee_WithSeededData_ReturnsExpectedResult()
    {
        var ct = TestContext.Current.CancellationToken;

        var ctx = await CreateTenantContextAsync(TenantRole.TenantAdmin, cancellationToken: ct);
        var user = await SeedUserAsync(cancellationToken: ct);
        var team = await SeedTeamAsync(ctx.Tenant, cancellationToken: ct);
        var employeeNumber = UniqueName("EMP");

        var json = await SendAuthorizedJsonAsync(
            HttpMethod.Post,
            "api/employees",
            ctx.Token,
            new
            {
                UserId = user.Id,
                EmployeeNumber = employeeNumber,
                FirstName = "Ada",
                LastName = "Lovelace",
                Email = user.Email,
                DateOfBirth = (DateOnly?)null,
                HireDate = (DateOnly?)null,
                TeamId = (long?)team.Id,
                ManagerEmployeeId = (long?)null,
                ExternalSourceId = (string?)null,
            },
            ct
        );

        Assert.Equal(user.Id, GetRequiredLong(json, "userId"));
        Assert.Equal(employeeNumber, GetRequiredString(json, "employeeNumber"));
    
    }
}
