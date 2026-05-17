using Domain.Interfaces;

namespace Domain.Entities;

public class AuditLogEntry : ITenantScoped
{
    public long Id { get; set; }
    public long? TenantId { get; set; }
    public long? ActorUserId { get; set; }
    public required string Action { get; set; }
    public required string EntityType { get; set; }
    public long? EntityId { get; set; }
    public DateTimeOffset OccurredOn { get; set; } = DateTimeOffset.UtcNow;
    public string? MetadataJson { get; set; }
}
