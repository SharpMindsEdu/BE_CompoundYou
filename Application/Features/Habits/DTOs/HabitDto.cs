namespace Application.Features.Habits.DTOs;

public record HabitDto(long Id, string Title, int Score, string? Description, string? Motivation);
