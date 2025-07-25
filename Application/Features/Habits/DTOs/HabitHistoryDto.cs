namespace Application.Features.Habits.DTOs;

public record HabitHistoryDto(
    long Id,
    long HabitId,
    long HabitHistoryId,
    long UserId,
    DateTime Date,
    bool IsCompleted,
    string? IsComment
);
