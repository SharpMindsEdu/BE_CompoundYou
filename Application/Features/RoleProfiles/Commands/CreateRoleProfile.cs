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

public static class CreateRoleProfile
{
    public const string Endpoint = "api/role-profiles";

    public record CreateRoleProfileCommand(
        long JobFamilyId,
        long CareerLevelId,
        string Name,
        string? Description
    ) : ICommandRequest<Result<RoleProfileDto>>, IAuditable
    {
        public string AuditAction => "role-profile.create";
        public string AuditEntityType => nameof(RoleProfile);
        public long? AuditEntityId => null;
    }

    public sealed class Validator : AbstractValidator<CreateRoleProfileCommand>
    {
        public Validator()
        {
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
        : IRequestHandler<CreateRoleProfileCommand, Result<RoleProfileDto>>
    {
        public async Task<Result<RoleProfileDto>> Handle(CreateRoleProfileCommand request, CancellationToken ct)
        {
            var family = await jobFamilies.GetById(request.JobFamilyId);
            if (family is null)
                return Result<RoleProfileDto>.Failure("Job family not found", ResultStatus.NotFound);

            var level = await careerLevels.GetById(request.CareerLevelId);
            if (level is null || level.JobFamilyId != request.JobFamilyId)
                return Result<RoleProfileDto>.Failure("Career level not found", ResultStatus.NotFound);

            if (await roleProfiles.Exist(x => x.Name == request.Name, ct))
                return Result<RoleProfileDto>.Failure("Role profile name already exists", ResultStatus.Conflict);

            var role = new RoleProfile
            {
                JobFamilyId = request.JobFamilyId,
                CareerLevelId = request.CareerLevelId,
                Name = request.Name.Trim(),
                Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            };
            await roleProfiles.Add(role);
            return Result<RoleProfileDto>.Success(ToDto(role, family, level));
        }
    }

    private static RoleProfileDto ToDto(RoleProfile role, JobFamily family, CareerLevel level) =>
        new(
            role.Id,
            role.JobFamilyId,
            family.Name,
            role.CareerLevelId,
            level.Name,
            level.Order,
            role.Name,
            role.Description,
            role.IsActive
        );
}

public sealed class CreateRoleProfileEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                CreateRoleProfile.Endpoint,
                async (CreateRoleProfile.CreateRoleProfileCommand command, ISender sender) =>
                    (await sender.Send(command)).ToHttpResult()
            )
            .RequireAuthorization(Policies.TenantAdmin)
            .Produces<RoleProfileDto>()
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict)
            .WithName("CreateRoleProfile")
            .WithTags("RoleProfiles");
    }
}
