namespace Domain.Interfaces;

/// <summary>
/// Marker for commands whose execution should be recorded in the audit log
/// by <c>AuditLogBehavior</c>.
/// </summary>
public interface IAuditable
{
    string AuditAction { get; }
    string AuditEntityType { get; }
    long? AuditEntityId { get; }
}
