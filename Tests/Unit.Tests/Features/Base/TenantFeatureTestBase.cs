using System.Security.Claims;
using Application.Shared;
using Domain.Entities;
using Domain.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace Unit.Tests.Features.Base;

public abstract class TenantFeatureTestBase(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper
) : FeatureTestBase(fixture, outputHelper)
{
    protected MutableCurrentTenant TenantContext { get; } = new();

    protected TenantFeatureTestBase(
        PostgreSqlRepositoryTestDatabaseFixture fixture,
        ITestOutputHelper outputHelper,
        long? tenantId,
        long? userId,
        long? membershipId,
        TenantRole? role,
        bool isPlatformAdmin
    )
        : this(fixture, outputHelper)
    {
        SetTenantContext(tenantId, userId, membershipId, role, isPlatformAdmin);
    }

    protected override async Task BuildServiceProvider(bool useMigrate = true)
    {
        Services.AddSingleton<ICurrentTenant>(TenantContext);
        await base.BuildServiceProvider(useMigrate);
    }

    protected Tenant SeedTenant(string slug = "tenant", string name = "Tenant")
    {
        var tenant = new Tenant { Name = name, Slug = slug };
        PersistWithDatabase(db => db.Add(tenant));
        return tenant;
    }

    protected User SeedUser(string displayName = "User", string? email = null)
    {
        var user = new User { DisplayName = displayName, Email = email };
        PersistWithDatabase(db => db.Add(user));
        return user;
    }

    protected void SetTenantContext(
        long? tenantId,
        long? userId = null,
        long? membershipId = null,
        TenantRole? role = TenantRole.TenantAdmin,
        bool isPlatformAdmin = false
    )
    {
        TenantContext.Set(tenantId, userId, membershipId, role, isPlatformAdmin);
    }

    public static ClaimsPrincipal CreatePrincipal(
        long? userId,
        long? tenantId,
        long? membershipId,
        TenantRole? role,
        bool isPlatformAdmin
    )
    {
        var claims = new List<Claim>();
        if (userId is not null)
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()));
        if (tenantId is not null)
            claims.Add(new Claim(CompoundYouClaimTypes.TenantId, tenantId.Value.ToString()));
        if (membershipId is not null)
            claims.Add(new Claim(CompoundYouClaimTypes.MembershipId, membershipId.Value.ToString()));
        if (role is not null)
            claims.Add(new Claim(CompoundYouClaimTypes.TenantRole, role.Value.ToString()));
        if (isPlatformAdmin)
            claims.Add(new Claim(CompoundYouClaimTypes.PlatformAdmin, "true"));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }
}

public sealed class MutableCurrentTenant : ICurrentTenant
{
    public long? TenantId { get; private set; }
    public long? UserId { get; private set; }
    public long? MembershipId { get; private set; }
    public TenantRole? Role { get; private set; }
    public bool IsPlatformAdmin { get; private set; }
    public ClaimsPrincipal? User { get; private set; }

    public void Set(
        long? tenantId,
        long? userId,
        long? membershipId,
        TenantRole? role,
        bool isPlatformAdmin,
        ClaimsPrincipal? user = null
    )
    {
        TenantId = tenantId;
        UserId = userId;
        MembershipId = membershipId;
        Role = role;
        IsPlatformAdmin = isPlatformAdmin;
        User = user ?? TenantFeatureTestBase.CreatePrincipal(
            userId,
            tenantId,
            membershipId,
            role,
            isPlatformAdmin
        );
    }
}
