using Application.Authorization;
using Application.Shared;
using Application.Shared.Extensions;
using Carter;
using Domain.Entities;
using Domain.Interfaces;
using Domain.Repositories;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Application.Features.EmployeeRoleProfiles.Commands;

public static class UnassignEmployeeRoleProfile
{
    public const string Endpoint = "api/employees/{employeeId:long}/role-profile";

    public record UnassignEmployeeRoleProfileCommand(long EmployeeId)
        : ICommandRequest<Result<bool>>,
            IAuditable
    {
        public string AuditAction => "employee-role-profile.unassign";
        public string AuditEntityType => nameof(EmployeeRoleProfile);
        public long? AuditEntityId => EmployeeId;
    }

    internal sealed class Handler(IRepository<EmployeeRoleProfile> assignments)
        : IRequestHandler<UnassignEmployeeRoleProfileCommand, Result<bool>>
    {
        public async Task<Result<bool>> Handle(UnassignEmployeeRoleProfileCommand request, CancellationToken ct)
        {
            var activeAssignments = await assignments.ListAll(
                x => x.EmployeeId == request.EmployeeId && x.IsActive,
                ct
            );
            foreach (var active in activeAssignments)
            {
                active.IsActive = false;
                assignments.Update(active);
            }

            return Result<bool>.Success(true);
        }
    }
}

public sealed class UnassignEmployeeRoleProfileEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete(
                UnassignEmployeeRoleProfile.Endpoint,
                async (long employeeId, ISender sender) =>
                    (await sender.Send(
                        new UnassignEmployeeRoleProfile.UnassignEmployeeRoleProfileCommand(employeeId)
                    )).ToHttpResult()
            )
            .RequireAuthorization(Policies.TenantAdmin)
            .Produces<bool>()
            .Produces(StatusCodes.Status404NotFound)
            .WithName("UnassignEmployeeRoleProfile")
            .WithTags("EmployeeRoleProfiles");
    }
}
