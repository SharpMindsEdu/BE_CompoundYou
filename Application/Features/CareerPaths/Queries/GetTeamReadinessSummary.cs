using Application.Authorization;
using Application.Features.Career.DTOs;
using Application.Features.CareerPaths.Services;
using Application.Shared;
using Application.Shared.Extensions;
using Carter;
using Domain.Entities;
using Domain.Repositories;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Application.Features.CareerPaths.Queries;

public static class GetTeamReadinessSummary
{
    public const string Endpoint = "api/career-paths/teams/{teamId:long}/readiness";

    public record GetTeamReadinessSummaryQuery(long TeamId)
        : IRequest<Result<TeamReadinessSummaryDto>>;

    internal sealed class Handler(
        IRepository<Employee> employees,
        ICareerReadinessService readinessService)
        : IRequestHandler<GetTeamReadinessSummaryQuery, Result<TeamReadinessSummaryDto>>
    {
        public async Task<Result<TeamReadinessSummaryDto>> Handle(
            GetTeamReadinessSummaryQuery request,
            CancellationToken ct
        )
        {
            var teamEmployees = await employees.ListAll(x => x.TeamId == request.TeamId && x.IsActive, ct);
            var rows = new List<TeamEmployeeReadinessDto>();

            foreach (var employee in teamEmployees.OrderBy(x => x.LastName).ThenBy(x => x.FirstName))
            {
                var careerPath = await readinessService.CalculateAsync(employee.Id, null, ct);
                rows.Add(
                    new TeamEmployeeReadinessDto(
                        employee.Id,
                        $"{employee.FirstName} {employee.LastName}".Trim(),
                        careerPath.CurrentRole,
                        careerPath.TargetRole,
                        careerPath.ReadinessScore,
                        careerPath.Band,
                        careerPath.SkillGaps.Count(x => x.Gap < 0)
                    )
                );
            }

            return Result<TeamReadinessSummaryDto>.Success(
                new TeamReadinessSummaryDto(request.TeamId, rows)
            );
        }
    }
}

public sealed class GetTeamReadinessSummaryEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                GetTeamReadinessSummary.Endpoint,
                async (long teamId, ISender sender) =>
                    (await sender.Send(new GetTeamReadinessSummary.GetTeamReadinessSummaryQuery(teamId))).ToHttpResult()
            )
            .RequireAuthorization(Policies.Manager)
            .Produces<TeamReadinessSummaryDto>()
            .WithName("GetTeamReadinessSummary")
            .WithTags("CareerPaths");
    }
}
