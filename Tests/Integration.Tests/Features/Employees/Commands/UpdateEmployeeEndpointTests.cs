using Domain.Enums;
using Application.Features.Employees.Commands;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Features.Employees.Commands;

[Trait("category", ServiceTestCategories.IntegrationTests)]
[Trait("category", ServiceTestCategories.EmployeeTests)]
public sealed class UpdateEmployeeEndpointTests(IntegrationTestStackFixture stack) : IntegrationTestBase(stack)
{
    [Fact]
    public async Task UpdateEmployee_WithoutBearerToken_ReturnsUnauthorized()
    {
        await AssertRequiresAuthenticationAsync(
            HttpMethod.Put,
            Route(UpdateEmployee.Endpoint, ("id", 1)),
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task UpdateEmployee_WithSeededData_ReturnsExpectedResult()
    {
        var ct = TestContext.Current.CancellationToken;

        var ctx = await CreateTenantContextAsync(TenantRole.Employee, cancellationToken: ct);
        Assert.NotNull(ctx.Employee);

        var json = await SendAuthorizedJsonAsync(
            HttpMethod.Put,
            Route("api/employees/{id:long}", ("id", ctx.Employee.Id)),
            ctx.Token,
            new
            {
                Id = ctx.Employee.Id,
                FirstName = "Grace",
                LastName = "Hopper",
                Email = ctx.User.Email,
                DateOfBirth = (DateOnly?)null,
                HireDate = (DateOnly?)null,
            },
            ct
        );

        Assert.Equal(ctx.Employee.Id, GetRequiredLong(json, "id"));
        Assert.Equal("Grace", GetRequiredString(json, "firstName"));
    
    }
}
