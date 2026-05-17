namespace Application.Features.EmployeeSkills.DTOs;

public record TeamHeatmapDto(
    long TeamId,
    List<EmployeeHeatmapDto> Employees
);

public record EmployeeHeatmapDto(
    long EmployeeId,
    string DisplayName,
    List<SkillHeatmapDto> Skills
);

public record SkillHeatmapDto(
    long SkillId,
    string SkillName,
    long? LevelId,
    string? LevelName,
    int? Order
);
