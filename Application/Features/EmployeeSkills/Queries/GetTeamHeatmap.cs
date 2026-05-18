using Application.Authorization;
using Application.Features.EmployeeSkills.DTOs;
using Application.Shared;
using Application.Shared.Extensions;
using Carter;
using Domain.Entities;
using Domain.Enums;
using Domain.Repositories;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.EmployeeSkills.Queries;

public static class GetTeamHeatmap
{
    public const string Endpoint = "api/employee-skills/teams/{teamId:long}/heatmap";

    public record GetTeamHeatmapQuery(long TeamId) : IRequest<Result<TeamHeatmapDto>>;

    internal sealed class Handler(
        IRepository<Employee> employees,
        IRepository<EmployeeSkillAssessment> assessments,
        IRepository<Skill> skills)
        : IRequestHandler<GetTeamHeatmapQuery, Result<TeamHeatmapDto>>
    {
        public async Task<Result<TeamHeatmapDto>> Handle(GetTeamHeatmapQuery request, CancellationToken ct)
        {
            // Simple validation: Ensure manager is in the same tenant (TenantId filter is automatic)
            var teamEmployees = await employees.ListAll(e => e.TeamId == request.TeamId, ct);
            
            if (teamEmployees.Count == 0)
                return Result<TeamHeatmapDto>.Success(new TeamHeatmapDto(request.TeamId, new List<EmployeeHeatmapDto>()));

            var employeeIds = teamEmployees.Select(e => e.Id).ToList();
            
            var teamAssessments = await assessments.ListAll(a => 
                employeeIds.Contains(a.EmployeeId) && a.Status == SkillAssessmentStatus.Validated, ct);
            
            var skillIds = teamAssessments.Select(a => a.SkillId).Distinct().ToList();
            var allSkills = await skills.ListAll(s => skillIds.Contains(s.Id), ct);

            var heatmapEmployees = new List<EmployeeHeatmapDto>();

            foreach (var emp in teamEmployees)
            {
                var empSkills = new List<SkillHeatmapDto>();
                foreach (var skill in allSkills)
                {
                    var assessment = teamAssessments.FirstOrDefault(a => a.EmployeeId == emp.Id && a.SkillId == skill.Id);
                    
                    // Level display data is resolved from the tenant-wide level system in richer views.
                    empSkills.Add(new SkillHeatmapDto(
                        skill.Id, 
                        skill.Name, 
                        assessment?.ValidatedSkillLevelId, 
                        null, // LevelName would require more includes/lookups
                        null  // Order would require more includes/lookups
                    ));
                }
                heatmapEmployees.Add(new EmployeeHeatmapDto(emp.Id, $"{emp.FirstName} {emp.LastName}", empSkills));
            }

            return Result<TeamHeatmapDto>.Success(new TeamHeatmapDto(request.TeamId, heatmapEmployees));
        }
    }
}

public class GetTeamHeatmapEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                GetTeamHeatmap.Endpoint,
                async (long teamId, ISender sender) =>
                {
                    var result = await sender.Send(new GetTeamHeatmap.GetTeamHeatmapQuery(teamId));
                    return result.ToHttpResult();
                }
            )
            .RequireAuthorization(Policies.Manager)
            .Produces<TeamHeatmapDto>()
            .WithName("GetTeamHeatmap")
            .WithTags("EmployeeSkills");
    }
}
