namespace Application.Features.Riftbound.DTOs;

public record RiftboundCardResponse(
    long Id,
    string ReferenceId,
    string Name,
    string? Effect,
    IReadOnlyCollection<string>? Color,
    int? Cost,
    string? Type,
    int? Might,
    IReadOnlyCollection<string>? Tags,
    string? SetName,
    string? Rarity,
    string? Cycle,
    string? Image,
    bool Promo
);
