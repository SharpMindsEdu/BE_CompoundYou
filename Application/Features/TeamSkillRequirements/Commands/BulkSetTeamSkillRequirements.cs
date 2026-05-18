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

namespace Application.Features.TeamSkillRequirements.Commands;

public static class BulkSetTeamSkillRequirements
{
    public const string Endpoint = "api/teams/{teamId:long}/skill-requirements";

    public record RequirementInput(long SkillId, long RequiredSkillLevelId, int Weight = 1);

    public record BulkSetTeamSkillRequirementsCommand(
        long TeamId,
        IReadOnlyList<RequirementInput> Requirements
    ) : ICommandRequest<Result<IReadOnlyList<TeamSkillRequirementDto>>>, IAuditable
    {
        public string AuditAction => "team.skill-requirements.bulk-set";
        public string AuditEntityType => nameof(Team);
        public long? AuditEntityId => TeamId;
    }

    public sealed class Validator : AbstractValidator<BulkSetTeamSkillRequirementsCommand>
    {
        public Validator()
        {
            RuleFor(x => x.TeamId).GreaterThan(0);
            RuleForEach(x => x.Requirements).ChildRules(r =>
            {
                r.RuleFor(x => x.SkillId).GreaterThan(0);
                r.RuleFor(x => x.RequiredSkillLevelId).GreaterThan(0);
                r.RuleFor(x => x.Weight).GreaterThan(0);
            });
        }
    }

    internal sealed class Handler(
        IRepository<Team> teams,
        IRepository<TeamSkillRequirement> requirements,
        IRepository<Skill> skills,
        IRepository<SkillLevel> skillLevels)
        : IRequestHandler<
            BulkSetTeamSkillRequirementsCommand,
            Result<IReadOnlyList<TeamSkillRequirementDto>>
        >
    {
        public async Task<Result<IReadOnlyList<TeamSkillRequirementDto>>> Handle(
            BulkSetTeamSkillRequirementsCommand request,
            CancellationToken ct
        )
        {
            if (!await teams.Exist(x => x.Id == request.TeamId, ct))
                return Result<IReadOnlyList<TeamSkillRequirementDto>>.Failure(
                    "Team not found",
                    ResultStatus.NotFound
                );

            var duplicateSkillId = request.Requirements
                .GroupBy(x => x.SkillId)
                .FirstOrDefault(x => x.Count() > 1)
                ?.Key;
            if (duplicateSkillId.HasValue)
                return Result<IReadOnlyList<TeamSkillRequirementDto>>.Failure(
                    $"Duplicate skill requirement for skill {duplicateSkillId.Value}",
                    ResultStatus.BadRequest
                );

            var existing = await requirements.ListAll(x => x.TeamId == request.TeamId, ct);
            if (existing.Count > 0)
                requirements.Remove(existing.ToArray());

            var rows = request.Requirements.Select(x => new TeamSkillRequirement
            {
                TeamId = request.TeamId,
                SkillId = x.SkillId,
                RequiredSkillLevelId = x.RequiredSkillLevelId,
                Weight = x.Weight,
            }).ToArray();

            if (rows.Length > 0)
                await requirements.Add(rows);

            return await BuildResultAsync(rows, skills, skillLevels, ct);
        }
    }

    internal static async Task<Result<IReadOnlyList<TeamSkillRequirementDto>>> BuildResultAsync(
        IReadOnlyList<TeamSkillRequirement> rows,
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
                return new TeamSkillRequirementDto(
                    x.Id,
                    x.TeamId,
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

        return Result<IReadOnlyList<TeamSkillRequirementDto>>.Success(dto);
    }
}

public sealed class BulkSetTeamSkillRequirementsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                BulkSetTeamSkillRequirements.Endpoint,
                async (
                    long teamId,
                    BulkSetTeamSkillRequirements.BulkSetTeamSkillRequirementsCommand body,
                    ISender sender
                ) => (await sender.Send(body with { TeamId = teamId })).ToHttpResult()
            )
            .RequireAuthorization(Policies.Manager)
            .Produces<IReadOnlyList<TeamSkillRequirementDto>>()
            .Produces(StatusCodes.Status404NotFound)
            .WithName("BulkSetTeamSkillRequirements")
            .WithTags("TeamSkillRequirements");
    }
}
