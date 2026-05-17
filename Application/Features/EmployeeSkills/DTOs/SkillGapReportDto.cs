namespace Application.Features.EmployeeSkills.DTOs;

public record SkillGapDto(
    long SkillId,
    string SkillName,
    int ActualLevelOrder,
    int RequiredLevelOrder,
    int Gap
);

public record SkillGapReportDto(
    long EmployeeId,
    List<SkillGapDto> Gaps
);
