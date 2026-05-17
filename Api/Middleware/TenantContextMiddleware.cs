using Application.Extensions;
using Application.Shared;

namespace Api.Middleware;

/// <summary>
/// Populates the per-request <see cref="ICurrentTenant"/> from JWT claims.
/// Must run AFTER UseAuthentication so claims are available, and BEFORE
/// any handler that touches tenant-scoped data.
/// </summary>
public sealed class TenantContextMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ICurrentTenant currentTenant)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            currentTenant.Set(
                tenantId: context.User.GetTenantId(),
                userId: context.User.GetUserId(),
                membershipId: context.User.GetMembershipId(),
                role: context.User.GetTenantRole(),
                isPlatformAdmin: context.User.IsPlatformAdmin()
            );
        }

        await next(context);
    }
}
