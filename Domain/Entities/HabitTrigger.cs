using Domain.Enums;

namespace Domain.Entities;

public class HabitTrigger : TrackedEntity
{
    public long Id { get; set; }
    public long HabitId { get; set; }
    /// <summary>
    /// Habit that activates this habit trigger
    /// </summary>
    public long? TriggerHabitId { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public HabitTriggerType Type { get; set; }
    public Habit Habit { get; set; } = null!;
    public Habit? TriggerHabit { get; set; }
}