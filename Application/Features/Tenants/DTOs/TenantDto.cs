using Domain.Enums;

namespace Application.Features.Tenants.DTOs;

public record TenantDto(
    long Id,
    string Name,
    string Slug,
    TenantStatus Status,
    string? Plan,
    long? OwnerUserId,
    DateTimeOffset CreatedOn
);

public record TenantOptionDto(long Id, string Slug, string Name, TenantRole Role);
