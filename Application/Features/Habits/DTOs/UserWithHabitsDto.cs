namespace Application.Features.Habits.DTOs;

public record UserWithHabitsDto(long Id, string Firstname, string Lastname, List<HabitDto> Habits);
