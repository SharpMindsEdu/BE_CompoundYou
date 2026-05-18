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

namespace Application.Features.RoleProfiles.Commands;

public static class UpdateRoleProfile
{
    public const string Endpoint = "api/role-profiles/{id:long}";

    public record UpdateRoleProfileCommand(
        long Id,
        long JobFamilyId,
        long CareerLevelId,
        string Name,
        string? Description,
        bool IsActive
    ) : ICommandRequest<Result<RoleProfileDto>>, IAuditable
    {
        public string AuditAction => "role-profile.update";
        public string AuditEntityType => nameof(RoleProfile);
        public long? AuditEntityId => Id;
    }

    public sealed class Validator : AbstractValidator<UpdateRoleProfileCommand>
    {
        public Validator()
        {
            RuleFor(x => x.Id).GreaterThan(0);
            RuleFor(x => x.JobFamilyId).GreaterThan(0);
            RuleFor(x => x.CareerLevelId).GreaterThan(0);
            RuleFor(x => x.Name).NotEmpty().MaximumLength(180);
            RuleFor(x => x.Description).MaximumLength(1500);
        }
    }

    internal sealed class Handler(
        IRepository<RoleProfile> roleProfiles,
        IRepository<JobFamily> jobFamilies,
        IRepository<CareerLevel> careerLevels)
        : IRequestHandler<UpdateRoleProfileCommand, Result<RoleProfileDto>>
    {
        public async Task<Result<RoleProfileDto>> Handle(UpdateRoleProfileCommand request, CancellationToken ct)
        {
            var role = await roleProfiles.GetById(request.Id);
            if (role is null)
                return Result<RoleProfileDto>.Failure("Role profile not found", ResultStatus.NotFound);

            var family = await jobFamilies.GetById(request.JobFamilyId);
            var level = await careerLevels.GetById(request.CareerLevelId);
            if (family is null || level is null || level.JobFamilyId != request.JobFamilyId)
                return Result<RoleProfileDto>.Failure("Career framework target not found", ResultStatus.NotFound);

            if (await roleProfiles.Exist(x => x.Id != request.Id && x.Name == request.Name, ct))
                return Result<RoleProfileDto>.Failure("Role profile name already exists", ResultStatus.Conflict);

            role.JobFamilyId = request.JobFamilyId;
            role.CareerLevelId = request.CareerLevelId;
            role.Name = request.Name.Trim();
            role.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
            role.IsActive = request.IsActive;
            roleProfiles.Update(role);

            return Result<RoleProfileDto>.Success(
                new RoleProfileDto(
                    role.Id,
                    role.JobFamilyId,
                    family.Name,
                    role.CareerLevelId,
                    level.Name,
                    level.Order,
                    role.Name,
                    role.Description,
                    role.IsActive
                )
            );
        }
    }
}

public sealed class UpdateRoleProfileEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                UpdateRoleProfile.Endpoint,
                async (long id, UpdateRoleProfile.UpdateRoleProfileCommand body, ISender sender) =>
                    (await sender.Send(body with { Id = id })).ToHttpResult()
            )
            .RequireAuthorization(Policies.TenantAdmin)
            .Produces<RoleProfileDto>()
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict)
            .WithName("UpdateRoleProfile")
            .WithTags("RoleProfiles");
    }
}
