namespace Application.Features.Skills.DTOs;

public record SkillNodeDto(
    long Id,
    string Name,
    string? Description,
    List<SkillNodeDto> Children
);
