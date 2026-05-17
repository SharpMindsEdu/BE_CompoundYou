using Domain.Enums;
using Microsoft.AspNetCore.Authorization;

namespace Application.Authorization;

/// <summary>
/// Requires the principal to have at least one of <paramref name="AllowedRoles"/>
/// in the current tenant context. Platform admins always satisfy the requirement.
/// </summary>
public sealed class TenantRoleRequirement(params TenantRole[] allowedRoles) : IAuthorizationRequirement
{
    public IReadOnlyCollection<TenantRole> AllowedRoles { get; } = allowedRoles;
}
