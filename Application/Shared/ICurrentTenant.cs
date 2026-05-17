using Domain.Enums;

namespace Application.Shared;

/// <summary>
/// Scoped accessor for the current request's tenant context. Populated by
/// <c>TenantContextMiddleware</c> from the JWT and consumed by the EF Core
/// global query filter and <c>TenantStampingInterceptor</c>. When the request
/// has no tenant in scope (anonymous, platform-admin endpoints, design-time
/// migrations) <c>TenantId</c> is null and tenant-scoped queries fall back
/// to the "global rows only" predicate (<c>TenantId IS NULL</c>).
/// </summary>
public interface ICurrentTenant
{
    long? TenantId { get; }
    long? UserId { get; }
    long? MembershipId { get; }
    TenantRole? Role { get; }
    bool IsPlatformAdmin { get; }
    bool HasTenant => TenantId.HasValue;

    void Set(long? tenantId, long? userId, long? membershipId, TenantRole? role, bool isPlatformAdmin);
}
