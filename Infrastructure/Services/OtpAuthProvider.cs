using Application.Features.Users.Services;
using Application.Shared;
using Domain.Entities;
using Domain.Repositories;

namespace Infrastructure.Services;

/// <summary>
/// Email/phone one-time-code provider. The OTP is set elsewhere (the
/// existing RequestLogin command) and stored in <c>User.SignInSecret</c>;
/// this provider validates and consumes it.
/// </summary>
public sealed class OtpAuthProvider(IRepository<User> users) : IAuthProvider
{
    public string ProviderName => "otp";

    public async Task<Result<User>> AuthenticateAsync(
        AuthRequest request,
        CancellationToken cancellationToken
    )
    {
        var user = await users.GetByExpression(
            u =>
                (request.Email != null && u.Email == request.Email)
                || (request.PhoneNumber != null && u.PhoneNumber == request.PhoneNumber),
            cancellationToken
        );

        if (user is null)
            return Result<User>.Failure(ErrorResults.EntityNotFound, ResultStatus.NotFound);

        if (user.SignInSecret is null)
            return Result<User>.Failure(ErrorResults.SignInNotFound, ResultStatus.Conflict);

        users.Update(user);

        if (!user.SignInSecret.Equals(request.Code))
        {
            user.SignInTries -= 1;
            if (user.SignInTries > 0)
                return Result<User>.Failure(
                    string.Format(ErrorResults.SignInCodeError, user.SignInTries),
                    ResultStatus.Conflict
                );

            user.SignInSecret = null;
            return Result<User>.Failure(ErrorResults.SignInFailed, ResultStatus.Conflict);
        }

        user.SignInSecret = null;
        return Result<User>.Success(user);
    }
}
