namespace Domain.Entities;

public class HabitTime
{
    public long Id { get; set; }
    public long HabitId { get; set; }
    public DayOfWeek Day { get; set; }
    public TimeSpan Time { get; set; }
    public Habit Habit { get; set; } = null!;
}
