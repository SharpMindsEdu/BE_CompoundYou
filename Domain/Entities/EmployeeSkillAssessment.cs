using Domain.Enums;
using Domain.Interfaces;

namespace Domain.Entities;

public class EmployeeSkillAssessment : TrackedEntity, ITenantScoped
{
    public long Id { get; set; }
    public long? TenantId { get; set; }
    public long EmployeeId { get; set; }
    public long SkillId { get; set; }
    public long ClaimedSkillLevelId { get; set; }
    public long? ValidatedSkillLevelId { get; set; }
    public long? ValidatedByEmployeeId { get; set; }
    public DateTimeOffset? ValidatedOn { get; set; }
    public SkillAssessmentStatus Status { get; set; } = SkillAssessmentStatus.SelfAssessed;
    public string? Evidence { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public Employee Employee { get; set; } = null!;
    public Skill Skill { get; set; } = null!;
    public SkillLevel ClaimedSkillLevel { get; set; } = null!;
    public SkillLevel? ValidatedSkillLevel { get; set; }
    public Employee? ValidatedByEmployee { get; set; }
}
