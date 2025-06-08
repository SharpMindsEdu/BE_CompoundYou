using Domain.Enums;

namespace Application.Features.Habits.DTOs;

public record HabitTriggerDto(
    long Id,
    string Title,
    string? Description,
    HabitTriggerType TriggerType
);
