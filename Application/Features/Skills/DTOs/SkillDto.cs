namespace Application.Features.Skills.DTOs;

public record SkillLevelDto(
    long Id,
    long SkillId,
    int Order,
    string Name,
    string? Description,
    int PointsThreshold
);

public record SkillDto(
    long Id,
    long? TenantId,
    long SkillCategoryId,
    string Name,
    string? Description,
    long? ParentSkillId,
    bool IsActive,
    List<SkillLevelDto> SkillLevels
);
