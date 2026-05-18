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
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Application.Features.EmployeeSkills.Commands;

public static class SubmitAssessment
{
    public const string Endpoint = "api/employee-skills/assessments";

    public record SubmitAssessmentCommand(long SkillId, long ClaimedSkillLevelId, string? Evidence) 
        : IRequest<Result<EmployeeSkillAssessmentDto>>, IAuditable
    {
        public string AuditAction => "employee_skill.submit_assessment";
        public string AuditEntityType => nameof(EmployeeSkillAssessment);
        public long? AuditEntityId => null;
    }

    internal sealed class Handler(
        IRepository<EmployeeSkillAssessment> assessments, 
        IRepository<Skill> skills,
        IRepository<SkillLevel> skillLevels,
        IRepository<Employee> employees,
        ICurrentTenant currentTenant)
        : IRequestHandler<SubmitAssessmentCommand, Result<EmployeeSkillAssessmentDto>>
    {
        public async Task<Result<EmployeeSkillAssessmentDto>> Handle(SubmitAssessmentCommand request, CancellationToken ct)
        {
            if (!currentTenant.UserId.HasValue)
                return Result<EmployeeSkillAssessmentDto>.Failure("User has no employee context", ResultStatus.Forbidden);

            var employee = await employees.GetByExpression(e => e.UserId == currentTenant.UserId.Value, ct);
            if (employee is null)
                return Result<EmployeeSkillAssessmentDto>.Failure("Employee profile not found in current tenant", ResultStatus.NotFound);

            var skill = await skills.GetById(request.SkillId);
            if (skill == null)
                return Result<EmployeeSkillAssessmentDto>.Failure("Skill not found", ResultStatus.NotFound);

            var level = await skillLevels.GetById(request.ClaimedSkillLevelId);
            if (level == null || level.SkillId != request.SkillId)
                return Result<EmployeeSkillAssessmentDto>.Failure("Invalid Skill Level for the selected Skill", ResultStatus.BadRequest);

            // Check for existing assessment for this skill
            var existing = await assessments.GetByExpression(a => 
                a.EmployeeId == employee.Id && a.SkillId == request.SkillId, ct);

            if (existing != null)
            {
                existing.ClaimedSkillLevelId = request.ClaimedSkillLevelId;
                existing.Evidence = request.Evidence;
                existing.Status = SkillAssessmentStatus.PendingValidation; // Automatically set to pending when re-submitted
                existing.ValidatedByEmployeeId = null;
                existing.ValidatedOn = null;
                existing.ValidatedSkillLevelId = null;
                
                assessments.Update(existing);
                await assessments.SaveChanges(ct);
                
                return Result<EmployeeSkillAssessmentDto>.Success(MapToDto(existing));
            }

            var assessment = new EmployeeSkillAssessment
            {
                EmployeeId = employee.Id,
                SkillId = request.SkillId,
                ClaimedSkillLevelId = request.ClaimedSkillLevelId,
                Evidence = request.Evidence,
                Status = SkillAssessmentStatus.PendingValidation,
                TenantId = currentTenant.TenantId
            };

            await assessments.Add(assessment);
            await assessments.SaveChanges(ct);

            return Result<EmployeeSkillAssessmentDto>.Success(MapToDto(assessment));
        }

        private static EmployeeSkillAssessmentDto MapToDto(EmployeeSkillAssessment a) =>
            new(a.Id, a.EmployeeId, a.SkillId, a.ClaimedSkillLevelId, a.ValidatedSkillLevelId, a.ValidatedByEmployeeId, a.ValidatedOn, a.Status, a.Evidence);
    }
}

public class SubmitAssessmentEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                SubmitAssessment.Endpoint,
                async (SubmitAssessment.SubmitAssessmentCommand command, ISender sender) =>
                {
                    var result = await sender.Send(command);
                    return result.ToHttpResult();
                }
            )
            .RequireAuthorization(Policies.Employee)
            .Produces<EmployeeSkillAssessmentDto>()
            .WithName("SubmitAssessment")
            .WithTags("EmployeeSkills");
    }
}
