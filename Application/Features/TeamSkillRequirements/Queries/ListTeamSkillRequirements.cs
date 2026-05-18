using Application.Authorization;
using Application.Features.Career.DTOs;
using Application.Features.TeamSkillRequirements.Commands;
using Application.Shared;
using Application.Shared.Extensions;
using Carter;
using Domain.Entities;
using Domain.Repositories;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Application.Features.TeamSkillRequirements.Queries;

public static class ListTeamSkillRequirements
{
    public const string Endpoint = "api/teams/{teamId:long}/skill-requirements";

    public record ListTeamSkillRequirementsQuery(long TeamId)
        : IRequest<Result<IReadOnlyList<TeamSkillRequirementDto>>>;

    internal sealed class Handler(
        IRepository<TeamSkillRequirement> requirements,
        IRepository<Skill> skills,
        IRepository<SkillLevel> skillLevels)
        : IRequestHandler<ListTeamSkillRequirementsQuery, Result<IReadOnlyList<TeamSkillRequirementDto>>>
    {
        public async Task<Result<IReadOnlyList<TeamSkillRequirementDto>>> Handle(
            ListTeamSkillRequirementsQuery request,
            CancellationToken ct
        )
        {
            var rows = await requirements.ListAll(x => x.TeamId == request.TeamId, ct);
            return await BulkSetTeamSkillRequirements.BuildResultAsync(rows, skills, skillLevels, ct);
        }
    }
}

public sealed class ListTeamSkillRequirementsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                ListTeamSkillRequirements.Endpoint,
                async (long teamId, ISender sender) =>
                    (await sender.Send(new ListTeamSkillRequirements.ListTeamSkillRequirementsQuery(teamId))).ToHttpResult()
            )
            .RequireAuthorization(Policies.Employee)
            .Produces<IReadOnlyList<TeamSkillRequirementDto>>()
            .WithName("ListTeamSkillRequirements")
            .WithTags("TeamSkillRequirements");
    }
}
