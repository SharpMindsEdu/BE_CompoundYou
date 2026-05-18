using Application.Authorization;
using Application.Features.EmployeeSkills.DTOs;
using Application.Shared;
using Application.Shared.Extensions;
using Carter;
using Domain.Entities;
using Domain.Interfaces;
using Domain.Enums;
using Domain.Repositories;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Application.Features.EmployeeSkills.Commands;

public static class RejectAssessment
{
    public const string Endpoint = "api/employee-skills/assessments/{id:long}/reject";

    public record RejectAssessmentCommand(long Id) : IRequest<Result<EmployeeSkillAssessmentDto>>, IAuditable
    {
        public string AuditAction => "employee_skill.reject_assessment";
        public string AuditEntityType => nameof(EmployeeSkillAssessment);
        public long? AuditEntityId => Id;
    }

    internal sealed class Handler(
        IRepository<EmployeeSkillAssessment> assessments, 
        IRepository<Employee> employees,
        ICurrentTenant currentTenant,
        IAuthorizationService authService)
        : IRequestHandler<RejectAssessmentCommand, Result<EmployeeSkillAssessmentDto>>
    {
        public async Task<Result<EmployeeSkillAssessmentDto>> Handle(RejectAssessmentCommand request, CancellationToken ct)
        {
            var assessment = await assessments.GetById(request.Id);
            if (assessment == null)
                return Result<EmployeeSkillAssessmentDto>.Failure("Assessment not found", ResultStatus.NotFound);

            var employee = await employees.GetById(assessment.EmployeeId);
            if (employee == null)
                return Result<EmployeeSkillAssessmentDto>.Failure("Target employee not found", ResultStatus.NotFound);

            var authResult = await authService.AuthorizeAsync(currentTenant.User!, employee, new EmployeeAccessRequirement());
            if (!authResult.Succeeded)
                return Result<EmployeeSkillAssessmentDto>.Failure(ErrorResults.Forbidden, ResultStatus.Forbidden);

            var actorEmployee = currentTenant.UserId.HasValue
                ? await employees.GetByExpression(e => e.UserId == currentTenant.UserId.Value, ct)
                : null;

            assessment.Status = SkillAssessmentStatus.Rejected;
            assessment.ValidatedByEmployeeId = actorEmployee?.Id;
            assessment.ValidatedOn = DateTimeOffset.UtcNow;

            assessments.Update(assessment);
            await assessments.SaveChanges(ct);

            return Result<EmployeeSkillAssessmentDto>.Success(new EmployeeSkillAssessmentDto(
                assessment.Id, assessment.EmployeeId, assessment.SkillId, assessment.ClaimedSkillLevelId, 
                assessment.ValidatedSkillLevelId, assessment.ValidatedByEmployeeId, assessment.ValidatedOn, 
                assessment.Status, assessment.Evidence));
        }
    }
}

public class RejectAssessmentEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                RejectAssessment.Endpoint,
                async (long id, ISender sender) =>
                {
                    var result = await sender.Send(new RejectAssessment.RejectAssessmentCommand(id));
                    return result.ToHttpResult();
                }
            )
            .RequireAuthorization(Policies.Manager)
            .Produces<EmployeeSkillAssessmentDto>()
            .WithName("RejectAssessment")
            .WithTags("EmployeeSkills");
    }
}
