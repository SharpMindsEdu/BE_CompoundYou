namespace Application.Features.Habits.DTOs;

public record HabitDto(
    long Id,
    string Title,
    int Score,
    string? Description,
    string? Motivation,
    List<HabitTimeDto>? Times = null,
    List<HabitHistoryDto>? History = null,
    List<HabitTriggerDto>? Triggers = null
);
