namespace Application.Features.Audit.DTOs;

public record AuditLogEntryDto(
    long Id,
    long? TenantId,
    long? ActorUserId,
    string Action,
    string EntityType,
    long? EntityId,
    DateTimeOffset OccurredOn,
    string? MetadataJson
);
