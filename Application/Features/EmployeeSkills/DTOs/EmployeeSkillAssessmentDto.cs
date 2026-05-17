using Domain.Enums;

namespace Application.Features.EmployeeSkills.DTOs;

public record EmployeeSkillAssessmentDto(
    long Id,
    long EmployeeId,
    long SkillId,
    long ClaimedSkillLevelId,
    long? ValidatedSkillLevelId,
    long? ValidatedByEmployeeId,
    DateTimeOffset? ValidatedOn,
    SkillAssessmentStatus Status,
    string? Evidence
);
