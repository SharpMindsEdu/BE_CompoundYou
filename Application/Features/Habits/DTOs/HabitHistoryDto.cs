namespace Application.Features.Habits.DTOs;

public record HabitHistoryDto(
    long Id,
    long HabitId,
    long UserId,
    DateTime Date,
    bool Completed,
    string? IsComment
);
