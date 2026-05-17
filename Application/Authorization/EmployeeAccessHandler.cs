using Application.Extensions;
using Domain.Entities;
using Domain.Enums;
using Domain.Repositories;
using Microsoft.AspNetCore.Authorization;

namespace Application.Authorization;

/// <summary>
/// Authorizes access to an <see cref="Employee"/> resource. Walks up the
/// <c>ManagerEmployeeId</c> chain (max 32 hops to bound cost) to detect
/// transitive management.
/// </summary>
public sealed class EmployeeAccessHandler(IRepository<Employee> employees)
    : AuthorizationHandler<EmployeeAccessRequirement, Employee>
{
    private const int MaxManagerHops = 32;

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        EmployeeAccessRequirement requirement,
        Employee resource
    )
    {
        if (context.User.IsPlatformAdmin())
        {
            context.Succeed(requirement);
            return;
        }

        var role = context.User.GetTenantRole();
        if (role is TenantRole.TenantAdmin)
        {
            context.Succeed(requirement);
            return;
        }

        var actorUserId = context.User.GetUserId();
        if (actorUserId is null)
            return;

        // Same person
        if (resource.UserId == actorUserId)
        {
            context.Succeed(requirement);
            return;
        }

        if (role is not TenantRole.Manager)
            return;

        // Find the manager's own Employee record in the same tenant
        var actorEmployee = await employees.GetByExpression(e => e.UserId == actorUserId.Value);
        if (actorEmployee is null)
            return;

        var currentManagerId = resource.ManagerEmployeeId;
        for (var hop = 0; hop < MaxManagerHops && currentManagerId is not null; hop++)
        {
            if (currentManagerId == actorEmployee.Id)
            {
                context.Succeed(requirement);
                return;
            }

            var upstream = await employees.GetById(currentManagerId.Value);
            if (upstream is null)
                return;
            currentManagerId = upstream.ManagerEmployeeId;
        }
    }
}
