using Application.Shared;
using Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Infrastructure.Interceptors;

/// <summary>
/// EF Core SaveChanges interceptor that enforces tenant isolation on writes:
///  - Added <see cref="ITenantScoped"/> rows have their TenantId stamped from
///    <see cref="ICurrentTenant"/> when the row leaves it unset.
///  - Modified rows whose TenantId differs from the current tenant (and the
///    actor is not a platform admin) are rejected to prevent cross-tenant
///    mutation via crafted requests.
///
/// Platform-owned rows (TenantId == null) are only writable when no tenant
/// is in scope OR by a platform admin; this matches the read-side filter
/// (<c>TenantId IS NULL || TenantId == currentTenant</c>).
/// </summary>
public sealed class TenantStampingInterceptor(ICurrentTenant currentTenant) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result
    )
    {
        EnforceTenant(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default
    )
    {
        EnforceTenant(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void EnforceTenant(DbContext? context)
    {
        if (context is null)
            return;

        var currentTenantId = currentTenant.TenantId;
        var isPlatformAdmin = currentTenant.IsPlatformAdmin;

        foreach (var entry in context.ChangeTracker.Entries<ITenantScoped>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    if (entry.Entity.TenantId is null)
                    {
                        if (currentTenantId is not null)
                        {
                            entry.Entity.TenantId = currentTenantId;
                        }
                        else if (!isPlatformAdmin)
                        {
                            throw new InvalidOperationException(
                                $"Cannot insert tenant-scoped entity '{entry.Metadata.Name}' "
                                    + "without a tenant in scope and the caller is not a platform admin."
                            );
                        }
                    }
                    else if (
                        currentTenantId is not null
                        && entry.Entity.TenantId != currentTenantId
                        && !isPlatformAdmin
                    )
                    {
                        ThrowCrossTenant(entry);
                    }
                    break;

                case EntityState.Modified:
                case EntityState.Deleted:
                    var originalTenantId = entry.OriginalValues[nameof(ITenantScoped.TenantId)] as long?;
                    if (
                        originalTenantId is not null
                        && currentTenantId is not null
                        && originalTenantId != currentTenantId
                        && !isPlatformAdmin
                    )
                    {
                        ThrowCrossTenant(entry);
                    }
                    break;
            }
        }
    }

    private static void ThrowCrossTenant(EntityEntry<ITenantScoped> entry)
    {
        throw new InvalidOperationException(
            $"Cross-tenant write blocked on '{entry.Metadata.Name}'. "
                + "TenantStampingInterceptor refused the change."
        );
    }
}
