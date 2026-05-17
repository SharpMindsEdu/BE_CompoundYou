namespace Domain.Interfaces;

/// <summary>
/// Marker for entities that belong to a single tenant. Rows are isolated
/// via a global query filter on <c>TenantId</c> and stamped on insert by
/// <c>TenantStampingInterceptor</c>. A nullable TenantId on the entity itself
/// signals "global/platform-owned" rows (e.g. shared skill library) that
/// every tenant can read.
/// </summary>
public interface ITenantScoped
{
    long? TenantId { get; set; }
}
