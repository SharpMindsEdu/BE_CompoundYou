using Domain.Enums;
using Domain.Interfaces;

namespace Domain.Entities;

public class LearningResource : TrackedEntity, ITenantScoped
{
    public long Id { get; set; }
    public long? TenantId { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public LearningResourceType Type { get; set; }
    public string? Url { get; set; }
    public long? MediaFileId { get; set; }
    public int? EstimatedMinutes { get; set; }
    public int PointsAwarded { get; set; }
    public bool IsActive { get; set; } = true;

    public Tenant? Tenant { get; set; }
}
