using Domain.Enums;

namespace Domain.Entities;

public class TenantInvitation : TrackedEntity
{
    public long Id { get; set; }
    public long TenantId { get; set; }
    public required string Email { get; set; }
    public TenantRole Role { get; set; }
    public required string Token { get; set; }
    public DateTimeOffset ExpiresOn { get; set; }
    public DateTimeOffset? AcceptedOn { get; set; }
    public long? AcceptedByUserId { get; set; }

    public Tenant Tenant { get; set; } = null!;
}
