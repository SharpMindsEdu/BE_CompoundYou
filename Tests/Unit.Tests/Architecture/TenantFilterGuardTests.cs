using Domain.Entities;
using Domain.Interfaces;
using Infrastructure;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Unit.Tests.Architecture;

/// <summary>
/// Guard test that fails CI if a tenant-scoped entity is added without a
/// global query filter wired up. Catches the most common multi-tenant
/// leak: an entity that implements <see cref="ITenantScoped"/> but is
/// missing from the reflection-driven filter setup in
/// <see cref="ApplicationDbContext.OnModelCreating"/>.
/// </summary>
public class TenantFilterGuardTests
{
    [Fact]
    public void Every_ITenantScoped_entity_has_a_global_query_filter()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=guard;Username=u;Password=p")
            .Options;
        using var context = new ApplicationDbContext(options, new CurrentTenant());

        var tenantScopedTypes = typeof(User)
            .Assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false } && typeof(ITenantScoped).IsAssignableFrom(t))
            .ToList();

        Assert.NotEmpty(tenantScopedTypes);

        var missing = new List<string>();
        foreach (var clrType in tenantScopedTypes)
        {
            var entityType = context.Model.FindEntityType(clrType);
            Assert.NotNull(entityType);

            if (entityType!.GetQueryFilter() is null)
            {
                missing.Add(clrType.FullName!);
            }
        }

        Assert.True(
            missing.Count == 0,
            $"ITenantScoped entities without a global query filter: {string.Join(", ", missing)}"
        );
    }
}
