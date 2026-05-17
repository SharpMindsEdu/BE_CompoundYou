using Application.Shared;
using Domain.Entities;

namespace Application.Features.Users.Services;

/// <summary>
/// Pluggable authentication provider. The current implementation is an
/// email/phone OTP flow (<c>OtpAuthProvider</c>); future providers (OIDC
/// for enterprise SSO) implement the same contract without requiring
/// changes to the Login pipeline.
/// </summary>
public interface IAuthProvider
{
    string ProviderName { get; }

    Task<Result<User>> AuthenticateAsync(AuthRequest request, CancellationToken cancellationToken);
}

public sealed record AuthRequest(string? Email, string? PhoneNumber, string Code);
