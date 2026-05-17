using Domain.Enums;

namespace Domain.Entities;

public class Tenant : TrackedEntity
{
    public long Id { get; set; }
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public TenantStatus Status { get; set; } = TenantStatus.Active;
    public string? Plan { get; set; }
    public long? OwnerUserId { get; set; }
    public User? OwnerUser { get; set; }
}
