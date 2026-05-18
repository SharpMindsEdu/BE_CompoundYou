using Application.Authorization;
using Application.Features.Career.DTOs;
using Application.Shared;
using Application.Shared.Extensions;
using Carter;
using Domain.Entities;
using Domain.Interfaces;
using Domain.Repositories;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Application.Features.EmployeeRoleProfiles.Commands;

public static class AssignEmployeeRoleProfile
{
    public const string Endpoint = "api/employees/{employeeId:long}/role-profile";

    public record AssignEmployeeRoleProfileCommand(long EmployeeId, long RoleProfileId)
        : ICommandRequest<Result<EmployeeRoleProfileDto>>,
            IAuditable
    {
        public string AuditAction => "employee-role-profile.assign";
        public string AuditEntityType => nameof(EmployeeRoleProfile);
        public long? AuditEntityId => null;
    }

    public sealed class Validator : AbstractValidator<AssignEmployeeRoleProfileCommand>
    {
        public Validator()
        {
            RuleFor(x => x.EmployeeId).GreaterThan(0);
            RuleFor(x => x.RoleProfileId).GreaterThan(0);
        }
    }

    internal sealed class Handler(
        IRepository<Employee> employees,
        IRepository<RoleProfile> roleProfiles,
        IRepository<EmployeeRoleProfile> assignments,
        IRepository<JobFamily> jobFamilies,
        IRepository<CareerLevel> careerLevels)
        : IRequestHandler<AssignEmployeeRoleProfileCommand, Result<EmployeeRoleProfileDto>>
    {
        public async Task<Result<EmployeeRoleProfileDto>> Handle(
            AssignEmployeeRoleProfileCommand request,
            CancellationToken ct
        )
        {
            if (!await employees.Exist(x => x.Id == request.EmployeeId, ct))
                return Result<EmployeeRoleProfileDto>.Failure("Employee not found", ResultStatus.NotFound);

            var role = await roleProfiles.GetById(request.RoleProfileId);
            if (role is null || !role.IsActive)
                return Result<EmployeeRoleProfileDto>.Failure("Role profile not found", ResultStatus.NotFound);

            var activeAssignments = await assignments.ListAll(
                x => x.EmployeeId == request.EmployeeId && x.IsActive,
                ct
            );
            foreach (var active in activeAssignments)
            {
                active.IsActive = false;
                assignments.Update(active);
            }

            var assignment = new EmployeeRoleProfile
            {
                EmployeeId = request.EmployeeId,
                RoleProfileId = request.RoleProfileId,
                AssignedOn = DateTimeOffset.UtcNow,
                IsActive = true,
            };
            await assignments.Add(assignment);

            var family = await jobFamilies.GetById(role.JobFamilyId);
            var level = await careerLevels.GetById(role.CareerLevelId);

            return Result<EmployeeRoleProfileDto>.Success(
                new EmployeeRoleProfileDto(
                    assignment.Id,
                    assignment.EmployeeId,
                    role.Id,
                    role.Name,
                    family?.Name,
                    level?.Name,
                    level?.Order,
                    assignment.AssignedOn,
                    assignment.IsActive
                )
            );
        }
    }
}

public sealed class AssignEmployeeRoleProfileEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                AssignEmployeeRoleProfile.Endpoint,
                async (
                    long employeeId,
                    AssignEmployeeRoleProfile.AssignEmployeeRoleProfileCommand body,
                    ISender sender
                ) => (await sender.Send(body with { EmployeeId = employeeId })).ToHttpResult()
            )
            .RequireAuthorization(Policies.TenantAdmin)
            .Produces<EmployeeRoleProfileDto>()
            .Produces(StatusCodes.Status404NotFound)
            .WithName("AssignEmployeeRoleProfile")
            .WithTags("EmployeeRoleProfiles");
    }
}
