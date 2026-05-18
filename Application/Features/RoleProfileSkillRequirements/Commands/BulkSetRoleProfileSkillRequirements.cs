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

namespace Application.Features.RoleProfileSkillRequirements.Commands;

public static class BulkSetRoleProfileSkillRequirements
{
    public const string Endpoint = "api/role-profiles/{roleProfileId:long}/skill-requirements";

    public record RequirementInput(long SkillId, long RequiredSkillLevelId, decimal Weight = 1m);

    public record BulkSetRoleProfileSkillRequirementsCommand(
        long RoleProfileId,
        IReadOnlyList<RequirementInput> Requirements
    ) : ICommandRequest<Result<IReadOnlyList<RoleProfileRequirementDto>>>, IAuditable
    {
        public string AuditAction => "role-profile.skill-requirements.bulk-set";
        public string AuditEntityType => nameof(RoleProfile);
        public long? AuditEntityId => RoleProfileId;
    }

    public sealed class Validator : AbstractValidator<BulkSetRoleProfileSkillRequirementsCommand>
    {
        public Validator()
        {
            RuleFor(x => x.RoleProfileId).GreaterThan(0);
            RuleForEach(x => x.Requirements).ChildRules(r =>
            {
                r.RuleFor(x => x.SkillId).GreaterThan(0);
                r.RuleFor(x => x.RequiredSkillLevelId).GreaterThan(0);
                r.RuleFor(x => x.Weight).GreaterThan(0);
            });
        }
    }

    internal sealed class Handler(
        IRepository<RoleProfile> roleProfiles,
        IRepository<RoleProfileSkillRequirement> requirements,
        IRepository<Skill> skills,
        IRepository<SkillLevel> skillLevels)
        : IRequestHandler<
            BulkSetRoleProfileSkillRequirementsCommand,
            Result<IReadOnlyList<RoleProfileRequirementDto>>
        >
    {
        public async Task<Result<IReadOnlyList<RoleProfileRequirementDto>>> Handle(
            BulkSetRoleProfileSkillRequirementsCommand request,
            CancellationToken ct
        )
        {
            if (!await roleProfiles.Exist(x => x.Id == request.RoleProfileId, ct))
                return Result<IReadOnlyList<RoleProfileRequirementDto>>.Failure(
                    "Role profile not found",
                    ResultStatus.NotFound
                );

            var duplicateSkillId = request.Requirements
                .GroupBy(x => x.SkillId)
                .FirstOrDefault(x => x.Count() > 1)
                ?.Key;
            if (duplicateSkillId.HasValue)
                return Result<IReadOnlyList<RoleProfileRequirementDto>>.Failure(
                    $"Duplicate skill requirement for skill {duplicateSkillId.Value}",
                    ResultStatus.BadRequest
                );

            var existing = await requirements.ListAll(x => x.RoleProfileId == request.RoleProfileId, ct);
            if (existing.Count > 0)
                requirements.Remove(existing.ToArray());

            var rows = request.Requirements.Select(x => new RoleProfileSkillRequirement
            {
                RoleProfileId = request.RoleProfileId,
                SkillId = x.SkillId,
                RequiredSkillLevelId = x.RequiredSkillLevelId,
                Weight = x.Weight,
            }).ToArray();

            if (rows.Length > 0)
                await requirements.Add(rows);

            return await BuildResultAsync(rows, skills, skillLevels, ct);
        }
    }

    internal static async Task<Result<IReadOnlyList<RoleProfileRequirementDto>>> BuildResultAsync(
        IReadOnlyList<RoleProfileSkillRequirement> rows,
        IRepository<Skill> skills,
        IRepository<SkillLevel> skillLevels,
        CancellationToken ct
    )
    {
        var skillIds = rows.Select(x => x.SkillId).Distinct().ToArray();
        var levelIds = rows.Select(x => x.RequiredSkillLevelId).Distinct().ToArray();
        var skillRows = skillIds.Length == 0 ? [] : await skills.ListAll(x => skillIds.Contains(x.Id), ct);
        var levelRows = levelIds.Length == 0 ? [] : await skillLevels.ListAll(x => levelIds.Contains(x.Id), ct);

        var dto = rows
            .Select(x =>
            {
                var skill = skillRows.FirstOrDefault(s => s.Id == x.SkillId);
                var level = levelRows.FirstOrDefault(l => l.Id == x.RequiredSkillLevelId);
                return new RoleProfileRequirementDto(
                    x.Id,
                    x.RoleProfileId,
                    x.SkillId,
                    skill?.Name ?? $"Skill #{x.SkillId}",
                    x.RequiredSkillLevelId,
                    level?.Name ?? $"Level #{x.RequiredSkillLevelId}",
                    level?.Order ?? 0,
                    x.Weight
                );
            })
            .OrderBy(x => x.SkillName)
            .ToList();

        return Result<IReadOnlyList<RoleProfileRequirementDto>>.Success(dto);
    }
}

public sealed class BulkSetRoleProfileSkillRequirementsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                BulkSetRoleProfileSkillRequirements.Endpoint,
                async (
                    long roleProfileId,
                    BulkSetRoleProfileSkillRequirements.BulkSetRoleProfileSkillRequirementsCommand body,
                    ISender sender
                ) => (await sender.Send(body with { RoleProfileId = roleProfileId })).ToHttpResult()
            )
            .RequireAuthorization(Policies.TenantAdmin)
            .Produces<IReadOnlyList<RoleProfileRequirementDto>>()
            .Produces(StatusCodes.Status404NotFound)
            .WithName("BulkSetRoleProfileSkillRequirements")
            .WithTags("RoleProfileSkillRequirements");
    }
}
