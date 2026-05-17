using Application.Extensions;
using Microsoft.AspNetCore.Authorization;

namespace Application.Authorization;

public sealed class TenantRoleHandler : AuthorizationHandler<TenantRoleRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        TenantRoleRequirement requirement
    )
    {
        if (context.User.IsPlatformAdmin())
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        var role = context.User.GetTenantRole();
        if (role is not null && requirement.AllowedRoles.Contains(role.Value))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
