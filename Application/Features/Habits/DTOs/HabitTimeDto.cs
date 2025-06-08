namespace Application.Features.Habits.DTOs;

public record HabitTimeDto(long Id, DayOfWeek Day, TimeSpan Time);
