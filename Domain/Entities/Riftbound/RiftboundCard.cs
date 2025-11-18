namespace Domain.Entities.Riftbound;

public class RiftboundCard
{
    public long Id { get; set; }
    public string ReferenceId { get; set; } = "";
    public string? Slug { get; set; }
    public string Name { get; set; } = "";
    public string? Effect { get; set; }
    public List<string>? Color { get; set; }
    public int? Cost { get; set; }
    public string? Type { get; set; }
    public int? Might { get; set; }
    public List<string>? Tags { get; set; }
    public string? SetName { get; set; }
    public string? Rarity { get; set; }
    public string? Cycle { get; set; }
    public string? Image { get; set; }
    public bool Promo { get; set; }
}
