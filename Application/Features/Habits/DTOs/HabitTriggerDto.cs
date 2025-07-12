using Domain.Enums;

namespace Application.Features.Habits.DTOs;

public record HabitTriggerDto(
    long Id,
    long HabitId,
    long TriggerHabitId,
    string Title,
    string? Description,
    HabitTriggerType Type
);