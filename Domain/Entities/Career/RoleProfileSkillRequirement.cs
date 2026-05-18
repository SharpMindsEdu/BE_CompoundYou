using Domain.Interfaces;

namespace Domain.Entities;

public class RoleProfileSkillRequirement : TrackedEntity, ITenantScoped
{
    public long Id { get; set; }
    public long? TenantId { get; set; }
    public long RoleProfileId { get; set; }
    public long SkillId { get; set; }
    public long RequiredSkillLevelId { get; set; }
    public decimal Weight { get; set; } = 1m;

    public Tenant Tenant { get; set; } = null!;
    public RoleProfile RoleProfile { get; set; } = null!;
    public Skill Skill { get; set; } = null!;
    public SkillLevel RequiredSkillLevel { get; set; } = null!;
}
