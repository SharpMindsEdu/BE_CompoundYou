namespace Domain.Entities;

/// <summary>
/// Tracks execution of habits
/// </summary>
public class HabitHistory
{
    public long Id { get; set; }
    public long HabitId { get; set; }
    public long UserId { get; set; }
    public long? HabitTimeId { get; set; }
    public DateTime Date { get; set; }
    public bool IsCompleted { get; set; }
    public string? Comment { get; set; }

    public Habit Habit { get; set; } = null!;
    public HabitTime? HabitTime { get; set; }
    public User User { get; set; } = null!;
}
