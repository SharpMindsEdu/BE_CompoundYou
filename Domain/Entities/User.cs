﻿using NpgsqlTypes;

namespace Domain.Entities;

/// <summary>
/// Represents a user/member in the system.
/// </summary>
public class User : TrackedEntity
{
    public long Id { get; set; }
    public required string DisplayName { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? SignInSecret { get; set; }
    public int? SignInTries { get; set; }
    public NpgsqlTsVector DisplayNameSearchVector { get; set; } = null!;
    public List<Habit> Habits { get; set; } = [];
    public List<HabitHistory> HabitHistory { get; set; } = [];
    public List<HabitTime> HabitTimes { get; set; } = [];
}
