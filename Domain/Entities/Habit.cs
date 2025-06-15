using NpgsqlTypes;

namespace Domain.Entities;

public class Habit : TrackedEntity
{
    public long Id { get; set; }
    public long? HabitPreparationId { get; set; }
    public long UserId { get; set; }
    public bool IsPreparationHabit { get; set; }
    public required string Title { get; set; }

    /// <summary>
    /// 0 is a bad habit and 100 is a good habit
    /// </summary>
    public int Score { get; set; }
    public string? Description { get; set; }
    public string? Motivation { get; set; }
    public NpgsqlTsVector TitleSearchVector { get; set; } = null!;

    /// <summary>
    /// Only set if this is a HabitPreparation
    /// </summary>
    public Habit Base { get; set; } = null!;

    public User User { get; set; } = null!;
    public List<Habit> Preparations { get; set; } = null!;

    /// <summary>
    /// An habit always consist of 4 habit triggers (Trigger, Craving, Action, Reward) - Except for HabitPreparations
    /// </summary>
    public List<HabitTrigger> Triggers { get; set; } = [];
    public List<HabitTime> Times { get; set; } = [];
    public List<HabitHistory> History { get; set; } = [];
}
