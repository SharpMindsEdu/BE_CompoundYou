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

public static class CopyRoleProfile
{
    public const string Endpoint = "api/role-profiles/{id:long}/copy";
    private const int MaxNameLength = 180;

    public record CopyRoleProfileCommand(long Id, string? Name = null)
        : ICommandRequest<Result<RoleProfileDto>>,
            IAuditable
    {
        public string AuditAction => "role-profile.copy";
        public string AuditEntityType => nameof(RoleProfile);
        public long? AuditEntityId => Id;
    }

    public sealed class Validator : AbstractValidator<CopyRoleProfileCommand>
    {
        public Validator()
        {
            RuleFor(x => x.Id).GreaterThan(0);
            RuleFor(x => x.Name).MaximumLength(MaxNameLength);
        }
    }

    internal sealed class Handler(
        IRepository<RoleProfile> roleProfiles,
        IRepository<RoleProfileSkillRequirement> requirements,
        IRepository<JobFamily> jobFamilies,
        IRepository<CareerLevel> careerLevels)
        : IRequestHandler<CopyRoleProfileCommand, Result<RoleProfileDto>>
    {
        public async Task<Result<RoleProfileDto>> Handle(CopyRoleProfileCommand request, CancellationToken ct)
        {
            var source = await roleProfiles.GetById(request.Id);
            if (source is null)
                return Result<RoleProfileDto>.Failure("Role profile not found", ResultStatus.NotFound);

            var family = await jobFamilies.GetById(source.JobFamilyId);
            var level = await careerLevels.GetById(source.CareerLevelId);
            if (family is null || level is null)
                return Result<RoleProfileDto>.Failure("Career framework target not found", ResultStatus.NotFound);

            var requestedName = string.IsNullOrWhiteSpace(request.Name) ? null : request.Name.Trim();
            if (requestedName is not null && await roleProfiles.Exist(x => x.Name == requestedName, ct))
                return Result<RoleProfileDto>.Failure("Role profile name already exists", ResultStatus.Conflict);

            var copyName = requestedName ?? await CreateUniqueCopyNameAsync(source.Name, roleProfiles, ct);
            var copy = new RoleProfile
            {
                JobFamilyId = source.JobFamilyId,
                CareerLevelId = source.CareerLevelId,
                Name = copyName,
                Description = source.Description,
                IsActive = source.IsActive,
            };

            await roleProfiles.Add(copy);
            await roleProfiles.SaveChanges(ct);

            var sourceRequirements = await requirements.ListAll(x => x.RoleProfileId == source.Id, ct);
            var copiedRequirements = sourceRequirements
                .Select(x => new RoleProfileSkillRequirement
                {
                    RoleProfileId = copy.Id,
                    SkillId = x.SkillId,
                    RequiredSkillLevelId = x.RequiredSkillLevelId,
                    Weight = x.Weight,
                })
                .ToArray();

            if (copiedRequirements.Length > 0)
                await requirements.Add(copiedRequirements);

            return Result<RoleProfileDto>.Success(
                new RoleProfileDto(
                    copy.Id,
                    copy.JobFamilyId,
                    family.Name,
                    copy.CareerLevelId,
                    level.Name,
                    level.Order,
                    copy.Name,
                    copy.Description,
                    copy.IsActive
                )
            );
        }

        private static async Task<string> CreateUniqueCopyNameAsync(
            string sourceName,
            IRepository<RoleProfile> roleProfiles,
            CancellationToken ct
        )
        {
            var candidate = ComposeCopyName(sourceName, null);
            var suffix = 2;
            while (await roleProfiles.Exist(x => x.Name == candidate, ct))
            {
                candidate = ComposeCopyName(sourceName, suffix);
                suffix++;
            }

            return candidate;
        }

        private static string ComposeCopyName(string sourceName, int? suffix)
        {
            var suffixText = suffix is null ? " Copy" : $" Copy {suffix}";
            var maxSourceLength = MaxNameLength - suffixText.Length;
            var trimmedSourceName = sourceName.Length <= maxSourceLength
                ? sourceName
                : sourceName[..maxSourceLength].TrimEnd();

            return $"{trimmedSourceName}{suffixText}";
        }
    }
}

public sealed class CopyRoleProfileEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                CopyRoleProfile.Endpoint,
                async (long id, CopyRoleProfile.CopyRoleProfileCommand body, ISender sender) =>
                    (await sender.Send(body with { Id = id })).ToHttpResult()
            )
            .RequireAuthorization(Policies.TenantAdmin)
            .Produces<RoleProfileDto>()
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict)
            .WithName("CopyRoleProfile")
            .WithTags("RoleProfiles");
    }
}
