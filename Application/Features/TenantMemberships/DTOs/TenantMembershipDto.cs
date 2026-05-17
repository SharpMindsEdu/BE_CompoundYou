using Domain.Enums;

namespace Application.Features.TenantMemberships.DTOs;

public record TenantMembershipDto(
    long Id,
    long TenantId,
    long UserId,
    TenantRole Role,
    DateTimeOffset JoinedOn,
    bool IsActive
);

public record TenantInvitationDto(
    long Id,
    long TenantId,
    string Email,
    TenantRole Role,
    string Token,
    DateTimeOffset ExpiresOn,
    DateTimeOffset? AcceptedOn
);
