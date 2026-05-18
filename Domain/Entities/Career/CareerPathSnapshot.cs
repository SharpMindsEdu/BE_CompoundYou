using Domain.Enums;
using Domain.Interfaces;

namespace Domain.Entities;

public class CareerPathSnapshot : TrackedEntity, ITenantScoped
{
    public long Id { get; set; }
    public long? TenantId { get; set; }
    public long EmployeeId { get; set; }
    public long? CurrentRoleProfileId { get; set; }
    public long? TargetRoleProfileId { get; set; }
    public int? ReadinessScore { get; set; }
    public int? SkillFitScore { get; set; }
    public int? ValidationCoverageScore { get; set; }
    public int? GoalCompletionScore { get; set; }
    public CareerReadinessBand? Band { get; set; }
    public DateTimeOffset ScoredOn { get; set; } = DateTimeOffset.UtcNow;

    public Tenant Tenant { get; set; } = null!;
    public Employee Employee { get; set; } = null!;
    public RoleProfile? CurrentRoleProfile { get; set; }
    public RoleProfile? TargetRoleProfile { get; set; }
}
