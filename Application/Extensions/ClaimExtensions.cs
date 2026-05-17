using System.Security.Claims;
using Application.Shared;
using Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace Application.Extensions;

public static class ClaimExtensions
{
    public static long? GetUserId(this HttpContext httpContext) =>
        httpContext.User.GetUserId();

    public static long? GetUserId(this ClaimsPrincipal principal)
    {
        var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return userId is null ? null : long.Parse(userId);
    }

    public static long? GetTenantId(this HttpContext httpContext) =>
        httpContext.User.GetTenantId();

    public static long? GetTenantId(this ClaimsPrincipal principal)
    {
        var tenantId = principal.FindFirst(CompoundYouClaimTypes.TenantId)?.Value;
        return tenantId is null ? null : long.Parse(tenantId);
    }

    public static long? GetMembershipId(this HttpContext httpContext) =>
        httpContext.User.GetMembershipId();

    public static long? GetMembershipId(this ClaimsPrincipal principal)
    {
        var membershipId = principal.FindFirst(CompoundYouClaimTypes.MembershipId)?.Value;
        return membershipId is null ? null : long.Parse(membershipId);
    }

    public static TenantRole? GetTenantRole(this HttpContext httpContext) =>
        httpContext.User.GetTenantRole();

    public static TenantRole? GetTenantRole(this ClaimsPrincipal principal)
    {
        var role = principal.FindFirst(CompoundYouClaimTypes.TenantRole)?.Value;
        return Enum.TryParse<TenantRole>(role, out var parsed) ? parsed : null;
    }

    public static bool IsPlatformAdmin(this HttpContext httpContext) =>
        httpContext.User.IsPlatformAdmin();

    public static bool IsPlatformAdmin(this ClaimsPrincipal principal) =>
        principal.FindFirst(CompoundYouClaimTypes.PlatformAdmin)?.Value == "true";
}
