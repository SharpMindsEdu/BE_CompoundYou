using Domain.Enums;

namespace Application.Features.Career.DTOs;

public record JobFamilyDto(
    long Id,
    string Name,
    string? Description,
    bool IsActive,
    DateTimeOffset CreatedOn
);

public record CareerLevelDto(
    long Id,
    long JobFamilyId,
    int Order,
    string Name,
    string? Description
);

public record RoleProfileDto(
    long Id,
    long JobFamilyId,
    string? JobFamilyName,
    long CareerLevelId,
    string? CareerLevelName,
    int? CareerLevelOrder,
    string Name,
    string? Description,
    bool IsActive
);

public record RoleProfileRequirementDto(
    long Id,
    long RoleProfileId,
    long SkillId,
    string SkillName,
    long RequiredSkillLevelId,
    string RequiredSkillLevelName,
    int RequiredSkillLevelOrder,
    decimal Weight
);

public record EmployeeRoleProfileDto(
    long Id,
    long EmployeeId,
    long RoleProfileId,
    string RoleProfileName,
    string? JobFamilyName,
    string? CareerLevelName,
    int? CareerLevelOrder,
    DateTimeOffset AssignedOn,
    bool IsActive
);

public record TeamSkillRequirementDto(
    long Id,
    long TeamId,
    long SkillId,
    string SkillName,
    long RequiredSkillLevelId,
    string RequiredSkillLevelName,
    int RequiredSkillLevelOrder,
    int Weight
);

public record CareerRoleSummaryDto(
    long Id,
    string Name,
    long JobFamilyId,
    string JobFamilyName,
    long CareerLevelId,
    string CareerLevelName,
    int CareerLevelOrder
);

public record CareerSkillGapDto(
    long SkillId,
    string SkillName,
    long RequiredSkillLevelId,
    string RequiredSkillLevelName,
    int RequiredSkillLevelOrder,
    long? ActualSkillLevelId,
    string? ActualSkillLevelName,
    int ActualSkillLevelOrder,
    int Gap,
    decimal Weight,
    bool IsMet
);

public record CareerPathDto(
    long EmployeeId,
    CareerRoleSummaryDto? CurrentRole,
    CareerRoleSummaryDto? TargetRole,
    int? ReadinessScore,
    int? SkillFitScore,
    int? ValidationCoverageScore,
    int? GoalCompletionScore,
    CareerReadinessBand? Band,
    IReadOnlyList<CareerSkillGapDto> SkillGaps,
    DateTimeOffset ScoredOn
);

public record TeamEmployeeReadinessDto(
    long EmployeeId,
    string DisplayName,
    CareerRoleSummaryDto? CurrentRole,
    CareerRoleSummaryDto? TargetRole,
    int? ReadinessScore,
    CareerReadinessBand? Band,
    int CriticalGapCount
);

public record TeamReadinessSummaryDto(
    long TeamId,
    IReadOnlyList<TeamEmployeeReadinessDto> Employees
);
