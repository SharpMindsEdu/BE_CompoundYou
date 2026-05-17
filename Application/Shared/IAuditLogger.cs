namespace Application.Shared;

public interface IAuditLogger
{
    Task LogAsync(
        string action,
        string entityType,
        long? entityId,
        string? metadataJson = null,
        CancellationToken cancellationToken = default
    );
}
