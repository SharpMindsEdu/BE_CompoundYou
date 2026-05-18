using Application.Authorization;
using Application.Features.EmployeeSkills.DTOs;
using Application.Shared;
using Application.Shared.Extensions;
using Application.Shared.Services;
using Carter;
using Domain.Entities;
using Domain.Enums;
using Domain.Repositories;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Application.Features.EmployeeSkills.Queries;

public static class GetSkillGapReport
{
    public const string Endpoint = "api/employee-skills/gap-report/{employeeId:long}";

    public record GetSkillGapReportQuery(long EmployeeId) : IRequest<Result<SkillGapReportDto>>;

    internal sealed class Handler(
        IRepository<Employee> employees,
        IRepository<EmployeeSkillAssessment> assessments,
        IRepository<Skill> skills,
        IRepository<SkillLevel> skillLevels,
        ITeamSkillRequirementProvider requirementProvider,
        ICurrentTenant currentTenant,
        IAuthorizationService authService)
        : IRequestHandler<GetSkillGapReportQuery, Result<SkillGapReportDto>>
    {
        public async Task<Result<SkillGapReportDto>> Handle(GetSkillGapReportQuery request, CancellationToken ct)
        {
            var employee = await employees.GetById(request.EmployeeId);
            if (employee == null)
                return Result<SkillGapReportDto>.Failure("Employee not found", ResultStatus.NotFound);

            var authResult = await authService.AuthorizeAsync(currentTenant.User!, employee, new EmployeeAccessRequirement());
            if (!authResult.Succeeded)
                return Result<SkillGapReportDto>.Failure(ErrorResults.Forbidden, ResultStatus.Forbidden);

            if (!employee.TeamId.HasValue)
                return Result<SkillGapReportDto>.Failure("Employee is not assigned to a team", ResultStatus.BadRequest);

            var requirements = await requirementProvider.GetRequirementsForTeamAsync(employee.TeamId.Value, ct);
            if (!requirements.Any())
            {
                // In Phase 2, this is expected since it's a mock. 
                // We'll return an empty report instead of an error.
                return Result<SkillGapReportDto>.Success(new SkillGapReportDto(request.EmployeeId, new List<SkillGapDto>()));
            }

            var employeeAssessments = await assessments.ListAll(a => 
                a.EmployeeId == request.EmployeeId && a.Status == SkillAssessmentStatus.Validated, ct);
            
            var gaps = new List<SkillGapDto>();

            foreach (var req in requirements)
            {
                var skill = await skills.GetById(req.SkillId);
                if (skill == null) continue;

                var reqLevel = await skillLevels.GetById(req.RequiredSkillLevelId);
                if (reqLevel == null) continue;

                var assessment = employeeAssessments.FirstOrDefault(a => a.SkillId == req.SkillId);
                int actualOrder = 0;
                
                if (assessment?.ValidatedSkillLevelId != null)
                {
                    var actualLevel = await skillLevels.GetById(assessment.ValidatedSkillLevelId.Value);
                    actualOrder = actualLevel?.Order ?? 0;
                }

                gaps.Add(new SkillGapDto(
                    skill.Id,
                    skill.Name,
                    actualOrder,
                    reqLevel.Order,
                    actualOrder - reqLevel.Order
                ));
            }

            return Result<SkillGapReportDto>.Success(new SkillGapReportDto(request.EmployeeId, gaps));
        }
    }
}

public class GetSkillGapReportEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                GetSkillGapReport.Endpoint,
                async (long employeeId, ISender sender) =>
                {
                    var result = await sender.Send(new GetSkillGapReport.GetSkillGapReportQuery(employeeId));
                    return result.ToHttpResult();
                }
            )
            .RequireAuthorization(Policies.Employee)
            .Produces<SkillGapReportDto>()
            .WithName("GetSkillGapReport")
            .WithTags("EmployeeSkills");
    }
}
