using Application.Shared;
using Domain.Entities;
using Domain.Repositories;

namespace Infrastructure.Services;

public sealed class AuditLogger(IRepository<AuditLogEntry> repository, ICurrentTenant currentTenant)
    : IAuditLogger
{
    public async Task LogAsync(
        string action,
        string entityType,
        long? entityId,
        string? metadataJson = null,
        CancellationToken cancellationToken = default
    )
    {
        await repository.Add(
            new AuditLogEntry
            {
                TenantId = currentTenant.TenantId,
                ActorUserId = currentTenant.UserId,
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                OccurredOn = DateTimeOffset.UtcNow,
                MetadataJson = metadataJson,
            }
        );
    }
}
