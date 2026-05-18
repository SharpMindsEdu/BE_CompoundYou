using Domain.Interfaces;

namespace Domain.Entities;

/// <summary>
/// Tenant-wide skill level used for every visible skill in a tenant.
/// SkillId is retained only for legacy rows while the schema is migrated; new code keeps it unset.
/// </summary>
public class SkillLevel : TrackedEntity, ITenantScoped
{
    public long Id { get; set; }
    public long? TenantId { get; set; }
    public long? SkillId { get; set; }
    public int Order { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public int PointsThreshold { get; set; }
    public bool IsActive { get; set; } = true;

    public Tenant? Tenant { get; set; }
}
