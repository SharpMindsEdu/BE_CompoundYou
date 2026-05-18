using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Application.Features.Users.Commands;
using Application.Features.Users.DTOs;
using Domain.Entities;
using Domain.Enums;
using Infrastructure;
using Infrastructure.Services;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;

namespace Integration.Tests.Infrastructure;

[Collection(IntegrationTestCollection.Name)]
public abstract partial class IntegrationTestBase(IntegrationTestStackFixture stack) : IAsyncLifetime
{
    private static long _uniqueCounter;

    protected IntegrationTestStackFixture Stack { get; } = stack;
    protected HttpClient Client { get; private set; } = null!;

    public virtual async ValueTask InitializeAsync()
    {
        Assert.SkipWhen(
            ShouldSkipIntegrationTests(),
            "Integration.Tests were explicitly skipped via COMPOUNDYOU_SKIP_INTEGRATION_TESTS=true."
        );

        await Stack.EnsureStartedAsync(TestContext.Current.CancellationToken);
        Client = Stack.CreateHttpClient();
        await Stack.ResetAsync(TestContext.Current.CancellationToken);
    }

    public virtual async ValueTask DisposeAsync()
    {
        Client?.Dispose();

        if (Stack.IsStarted)
        {
            await Stack.ResetAsync(CancellationToken.None);
        }
    }

    protected ApplicationDbContext CreateDbContext(
        long? tenantId = null,
        long? userId = null,
        long? membershipId = null,
        TenantRole? role = null,
        bool isPlatformAdmin = false
    )
    {
        var currentTenant = new CurrentTenant();
        if (
            tenantId is not null
            || userId is not null
            || membershipId is not null
            || role is not null
            || isPlatformAdmin
        )
        {
            currentTenant.Set(tenantId, userId, membershipId, role, isPlatformAdmin);
        }

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(Stack.DatabaseConnectionString)
            .Options;

        return new ApplicationDbContext(options, currentTenant);
    }

    private static bool ShouldSkipIntegrationTests()
    {
        var value = Environment.GetEnvironmentVariable("COMPOUNDYOU_SKIP_INTEGRATION_TESTS");
        return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "1", StringComparison.OrdinalIgnoreCase);
    }

    protected async Task<T> ReadJsonAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken = default
    )
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        Assert.True(
            response.IsSuccessStatusCode,
            $"Expected success status code but got {(int)response.StatusCode} {response.StatusCode}. Body: {body}"
        );

        var value = JsonSerializer.Deserialize<T>(body, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(value);
        return value;
    }

    protected async Task AssertRequiresAuthenticationAsync(
        HttpMethod method,
        string requestUri,
        CancellationToken cancellationToken = default
    )
    {
        using var request = new HttpRequestMessage(method, requestUri);
        if (method == HttpMethod.Post || method == HttpMethod.Put)
        {
            request.Content = JsonContent.Create(new { });
        }

        using var response = await Client.SendAsync(request, cancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    protected static string Route(
        string endpoint,
        params (string Name, long Value)[] routeValues
    )
    {
        var route = endpoint;
        foreach (var (name, value) in routeValues)
        {
            var valueText = value.ToString(CultureInfo.InvariantCulture);
            route = route
                .Replace($"{{{name}:long}}", valueText, StringComparison.Ordinal)
                .Replace($"{{{name}}}", valueText, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("{", route);
        return route;
    }

    protected static string UniqueName(string prefix)
    {
        var value = Interlocked.Increment(ref _uniqueCounter);
        return TrimUnique($"{prefix}-{value}-{Guid.NewGuid():N}", 48);
    }

    protected static string UniqueEmail(string prefix = "user")
    {
        var value = Interlocked.Increment(ref _uniqueCounter);
        return $"{prefix}-{value}-{Guid.NewGuid():N}@integration.test";
    }

    protected static string UniqueSlug(string prefix)
    {
        var value = Interlocked.Increment(ref _uniqueCounter);
        return TrimUnique($"{prefix}-{value}-{Guid.NewGuid():N}", 64);
    }

    private static string TrimUnique(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    protected async Task<User> SeedUserAsync(
        string? displayName = null,
        string? email = null,
        string? phoneNumber = null,
        bool isPlatformAdmin = false,
        string? signInSecret = null,
        int? signInTries = null,
        CancellationToken cancellationToken = default
    )
    {
        await using var db = CreateDbContext();
        var user = new User
        {
            DisplayName = displayName ?? UniqueName("User"),
            Email = email ?? UniqueEmail(),
            PhoneNumber = phoneNumber,
            IsPlatformAdmin = isPlatformAdmin,
            SignInSecret = signInSecret,
            SignInTries = signInTries,
        };

        db.Add(user);
        await db.SaveChangesAsync(cancellationToken);
        return user;
    }

    protected async Task<Tenant> SeedTenantAsync(
        string? name = null,
        string? slug = null,
        long? ownerUserId = null,
        TenantStatus status = TenantStatus.Active,
        string? plan = null,
        CancellationToken cancellationToken = default
    )
    {
        await using var db = CreateDbContext();
        var tenant = new Tenant
        {
            Name = name ?? UniqueName("Tenant"),
            Slug = slug ?? UniqueSlug("tenant"),
            OwnerUserId = ownerUserId,
            Status = status,
            Plan = plan,
        };

        db.Add(tenant);
        await db.SaveChangesAsync(cancellationToken);
        return tenant;
    }

    protected async Task<TenantMembership> SeedTenantMembershipAsync(
        Tenant tenant,
        User user,
        TenantRole role = TenantRole.Employee,
        bool isActive = true,
        CancellationToken cancellationToken = default
    )
    {
        await using var db = CreateDbContext();
        var membership = new TenantMembership
        {
            TenantId = tenant.Id,
            UserId = user.Id,
            Role = role,
            IsActive = isActive,
        };

        db.Add(membership);
        await db.SaveChangesAsync(cancellationToken);
        return membership;
    }

    protected async Task<SeededTenantMember> SeedTenantWithMemberAsync(
        TenantRole role = TenantRole.Employee,
        CancellationToken cancellationToken = default
    )
    {
        var user = await SeedUserAsync(cancellationToken: cancellationToken);
        var tenant = await SeedTenantAsync(ownerUserId: user.Id, cancellationToken: cancellationToken);
        var membership = await SeedTenantMembershipAsync(
            tenant,
            user,
            role,
            cancellationToken: cancellationToken
        );

        return new SeededTenantMember(tenant, user, membership);
    }

    protected async Task<Department> SeedDepartmentAsync(
        Tenant tenant,
        string? name = null,
        Department? parentDepartment = null,
        CancellationToken cancellationToken = default
    )
    {
        await using var db = CreateDbContext(tenant.Id);
        var department = new Department
        {
            TenantId = tenant.Id,
            Name = name ?? UniqueName("Department"),
            ParentDepartmentId = parentDepartment?.Id,
        };

        db.Add(department);
        await db.SaveChangesAsync(cancellationToken);
        return department;
    }

    protected async Task<Team> SeedTeamAsync(
        Tenant tenant,
        Department? department = null,
        string? name = null,
        Employee? manager = null,
        CancellationToken cancellationToken = default
    )
    {
        department ??= await SeedDepartmentAsync(tenant, cancellationToken: cancellationToken);

        await using var db = CreateDbContext(tenant.Id);
        var team = new Team
        {
            TenantId = tenant.Id,
            DepartmentId = department.Id,
            Name = name ?? UniqueName("Team"),
            ManagerEmployeeId = manager?.Id,
        };

        db.Add(team);
        await db.SaveChangesAsync(cancellationToken);
        return team;
    }

    protected async Task<Employee> SeedEmployeeAsync(
        Tenant tenant,
        User? user = null,
        Team? team = null,
        Employee? manager = null,
        string? employeeNumber = null,
        string? firstName = null,
        string? lastName = null,
        string? email = null,
        bool isActive = true,
        CancellationToken cancellationToken = default
    )
    {
        user ??= await SeedUserAsync(cancellationToken: cancellationToken);

        await using var db = CreateDbContext(tenant.Id, user.Id);
        var employee = new Employee
        {
            TenantId = tenant.Id,
            UserId = user.Id,
            TeamId = team?.Id,
            ManagerEmployeeId = manager?.Id,
            EmployeeNumber = employeeNumber ?? UniqueName("EMP"),
            FirstName = firstName ?? "Integration",
            LastName = lastName ?? "Employee",
            Email = email ?? user.Email ?? UniqueEmail("employee"),
            IsActive = isActive,
        };

        db.Add(employee);
        await db.SaveChangesAsync(cancellationToken);
        return employee;
    }

    protected async Task<string> LoginAsync(
        User user,
        CancellationToken cancellationToken = default
    )
    {
        var requestLoginResponse = await Client.PutAsJsonAsync(
            RequestLogin.Endpoint,
            new RequestLogin.RequestLoginCommand(user.Email, user.PhoneNumber),
            cancellationToken
        );
        Assert.True(requestLoginResponse.IsSuccessStatusCode);

        var loginResponse = await Client.PutAsJsonAsync(
            Login.Endpoint,
            new Login.LoginCommand("123456", user.Email, user.PhoneNumber),
            cancellationToken
        );
        var token = await ReadJsonAsync<TokenDto>(loginResponse, cancellationToken);
        Assert.False(string.IsNullOrWhiteSpace(token.Token));
        return token.Token;
    }

    protected HttpRequestMessage CreateAuthorizedRequest(
        HttpMethod method,
        string requestUri,
        string token
    )
    {
        var request = new HttpRequestMessage(method, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    protected HubConnection CreateHubConnection(string hubPath, string token)
    {
        var path = hubPath.TrimStart('/');
        var hubUri = new Uri(Stack.ApiBaseAddress, path);
        return new HubConnectionBuilder()
            .WithUrl(hubUri, options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .Build();
    }

    protected HubConnection CreateChatHubConnection(string token) =>
        CreateHubConnection("/chatHub", token);

    protected HubConnection CreateMatrixHubConnection(string token) =>
        CreateHubConnection("/matrixHub", token);

    protected sealed record SeededTenantMember(
        Tenant Tenant,
        User User,
        TenantMembership Membership
    );
}
