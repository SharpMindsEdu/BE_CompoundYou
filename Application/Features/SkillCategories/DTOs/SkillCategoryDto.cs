namespace Application.Features.SkillCategories.DTOs;

public record SkillCategoryDto(
    long Id,
    long? TenantId,
    string Name,
    string? Description,
    bool IsActive
);
