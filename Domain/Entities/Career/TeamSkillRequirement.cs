using Domain.Interfaces;

namespace Domain.Entities;

public class TeamSkillRequirement : TrackedEntity, ITenantScoped
{
    public long Id { get; set; }
    public long? TenantId { get; set; }
    public long TeamId { get; set; }
    public long SkillId { get; set; }
    public long RequiredSkillLevelId { get; set; }
    public int Weight { get; set; } = 1;

    public Tenant Tenant { get; set; } = null!;
    public Team Team { get; set; } = null!;
    public Skill Skill { get; set; } = null!;
    public SkillLevel RequiredSkillLevel { get; set; } = null!;
}
