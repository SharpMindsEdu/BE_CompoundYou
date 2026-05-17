namespace Domain.Entities;

/// <summary>
/// Tier on a Skill's ordered scale (e.g. 1 = Beginner, 2 = Advanced, 3 = Expert).
/// Tenant scoping is implicit via the parent <see cref="Skill"/> — direct
/// queries against SkillLevel are typically scoped through Skill navigation,
/// so SkillLevel does not carry its own TenantId.
/// </summary>
public class SkillLevel : TrackedEntity
{
    public long Id { get; set; }
    public long SkillId { get; set; }
    public int Order { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public int PointsThreshold { get; set; }

    public Skill Skill { get; set; } = null!;
}
