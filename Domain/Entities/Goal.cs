using Domain.Enums;
using Domain.Interfaces;

namespace Domain.Entities;

public class Goal : TrackedEntity, ITenantScoped
{
    public long Id { get; set; }
    public long? TenantId { get; set; }
    public long EmployeeId { get; set; }
    public long AuthorEmployeeId { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public GoalPeriod Period { get; set; }
    public GoalTargetType TargetType { get; set; }
    public decimal? TargetValue { get; set; }
    public decimal? CurrentValue { get; set; }
    public DateOnly? DueOn { get; set; }
    public GoalStatus Status { get; set; } = GoalStatus.Draft;
    public long? TargetSkillId { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public Employee Employee { get; set; } = null!;
    public Employee AuthorEmployee { get; set; } = null!;
    public Skill? TargetSkill { get; set; }
    public ICollection<GoalCheckIn> CheckIns { get; set; } = new List<GoalCheckIn>();
}
