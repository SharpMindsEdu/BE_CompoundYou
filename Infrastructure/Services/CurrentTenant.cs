using Application.Shared;
using Domain.Enums;

namespace Infrastructure.Services;

/// <summary>
/// Default scoped implementation populated once per request by
/// <c>TenantContextMiddleware</c>. Immutable after Set is called; calling
/// Set twice in the same scope throws to prevent silent tenant switching
/// mid-request.
/// </summary>
public sealed class CurrentTenant : ICurrentTenant
{
    private bool _assigned;

    public long? TenantId { get; private set; }
    public long? UserId { get; private set; }
    public long? MembershipId { get; private set; }
    public TenantRole? Role { get; private set; }
    public bool IsPlatformAdmin { get; private set; }
    public System.Security.Claims.ClaimsPrincipal? User { get; private set; }

    public void Set(
        long? tenantId,
        long? userId,
        long? membershipId,
        TenantRole? role,
        bool isPlatformAdmin,
        System.Security.Claims.ClaimsPrincipal? user = null
    )
    {
        if (_assigned)
            throw new InvalidOperationException(
                "CurrentTenant has already been assigned for this scope."
            );

        TenantId = tenantId;
        UserId = userId;
        MembershipId = membershipId;
        Role = role;
        IsPlatformAdmin = isPlatformAdmin;
        User = user;
        _assigned = true;
    }
}
