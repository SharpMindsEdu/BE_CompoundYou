using Application.Authorization;
using Application.Features.EmployeeSkills.DTOs;
using Application.Features.Skills.Services;
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

public static class ValidateAssessment
{
    public const string Endpoint = "api/employee-skills/assessments/{id:long}/validate";

    public record ValidateAssessmentCommand(long Id, long? ValidatedSkillLevelId = null) 
        : IRequest<Result<EmployeeSkillAssessmentDto>>, IAuditable
    {
        public string AuditAction => "employee_skill.validate_assessment";
        public string AuditEntityType => nameof(EmployeeSkillAssessment);
        public long? AuditEntityId => Id;
    }

    public record AssessmentValidatedNotification(EmployeeSkillAssessment Assessment) : INotification;

    internal sealed class Handler(
        IRepository<EmployeeSkillAssessment> assessments, 
        IRepository<Employee> employees,
        IRepository<SkillLevel> skillLevels,
        ICurrentTenant currentTenant,
        IAuthorizationService authService,
        IMediator mediator)
        : IRequestHandler<ValidateAssessmentCommand, Result<EmployeeSkillAssessmentDto>>
    {
        public async Task<Result<EmployeeSkillAssessmentDto>> Handle(ValidateAssessmentCommand request, CancellationToken ct)
        {
            var assessment = await assessments.GetById(request.Id);
            if (assessment == null)
                return Result<EmployeeSkillAssessmentDto>.Failure("Assessment not found", ResultStatus.NotFound);

            var employee = await employees.GetById(assessment.EmployeeId);
            if (employee == null)
                return Result<EmployeeSkillAssessmentDto>.Failure("Target employee not found", ResultStatus.NotFound);

            // Authorization: Use EmployeeAccessHandler to check if the actor can manage this employee
            var authResult = await authService.AuthorizeAsync(currentTenant.User!, employee, new EmployeeAccessRequirement());
            if (!authResult.Succeeded)
                return Result<EmployeeSkillAssessmentDto>.Failure(ErrorResults.Forbidden, ResultStatus.Forbidden);

            // Cannot validate own assessment (EmployeeAccessHandler would allow same person, but business rule forbids)
            if (employee.UserId == currentTenant.UserId && !currentTenant.IsPlatformAdmin)
                return Result<EmployeeSkillAssessmentDto>.Failure("Managers cannot validate their own assessments", ResultStatus.Forbidden);

            var actorEmployee = currentTenant.UserId.HasValue
                ? await employees.GetByExpression(e => e.UserId == currentTenant.UserId.Value, ct)
                : null;

            var validatedSkillLevelId = request.ValidatedSkillLevelId ?? assessment.ClaimedSkillLevelId;
            var levelResult = await SkillLevelUsage.GetUsableTenantLevelAsync(
                skillLevels,
                currentTenant,
                validatedSkillLevelId,
                ct
            );
            if (!levelResult.Succeeded)
                return Result<EmployeeSkillAssessmentDto>.Failure(
                    levelResult.ErrorMessage ?? "Invalid skill level for the selected skill",
                    levelResult.Status
                );

            assessment.Status = SkillAssessmentStatus.Validated;
            assessment.ValidatedSkillLevelId = validatedSkillLevelId;
            assessment.ValidatedByEmployeeId = actorEmployee?.Id;
            assessment.ValidatedOn = DateTimeOffset.UtcNow;

            assessments.Update(assessment);
            await assessments.SaveChanges(ct);

            await mediator.Publish(new AssessmentValidatedNotification(assessment), ct);

            return Result<EmployeeSkillAssessmentDto>.Success(new EmployeeSkillAssessmentDto(
                assessment.Id, assessment.EmployeeId, assessment.SkillId, assessment.ClaimedSkillLevelId, 
                assessment.ValidatedSkillLevelId, assessment.ValidatedByEmployeeId, assessment.ValidatedOn, 
                assessment.Status, assessment.Evidence));
        }
    }
}

public class ValidateAssessmentEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                ValidateAssessment.Endpoint,
                async (long id, ValidateAssessment.ValidateAssessmentCommand? body, ISender sender) =>
                {
                    var result = await sender.Send((body ?? new ValidateAssessment.ValidateAssessmentCommand(id)) with { Id = id });
                    return result.ToHttpResult();
                }
            )
            .RequireAuthorization(Policies.Manager)
            .Produces<EmployeeSkillAssessmentDto>()
            .WithName("ValidateAssessment")
            .WithTags("EmployeeSkills");
    }
}
