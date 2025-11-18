using Application.Features.Users.DTOs;
using Application.Features.Users.Services;
using Application.Shared;
using Application.Shared.Extensions;
using Carter;
using Domain.Entities;
using Domain.Repositories;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Application.Features.Users.Commands;

/// <summary>
/// Upon 3 faulty entries a new code will be sent
/// During testing it will be simply 123456
/// </summary>
public static class Login
{
    public const string Endpoint = "api/users/login";

    public record LoginCommand(string Code, string? Email, string? PhoneNumber)
        : ICommandRequest<Result<TokenDto>>;

    public class Validator : AbstractValidator<LoginCommand>
    {
        public Validator()
        {
            RuleFor(x => x.Code).NotEmpty().Length(6);
            RuleFor(x => x.Email)
                .Must(
                    (cmd, email) =>
                        !string.IsNullOrEmpty(email) || !string.IsNullOrEmpty(cmd.PhoneNumber)
                )
                .WithMessage(ValidationErrors.EmailAndPhoneNumberMissing);
        }
    }

    internal sealed class Handler(IRepository<User> repo, ITokenService tokenService)
        : IRequestHandler<LoginCommand, Result<TokenDto>>
    {
        public async Task<Result<TokenDto>> Handle(LoginCommand request, CancellationToken ct)
        {
            var existingUser = await repo.GetByExpression(
                x =>
                    (request.Email != null && x.Email == request.Email)
                    || (request.PhoneNumber != null && x.PhoneNumber == request.PhoneNumber),
                ct
            );

            if (existingUser == null)
                return Result<TokenDto>.Failure(ErrorResults.EntityNotFound, ResultStatus.NotFound);

            if (existingUser.SignInSecret is null)
            {
                return Result<TokenDto>.Failure(ErrorResults.SignInNotFound, ResultStatus.Conflict);
            }

            repo.Update(existingUser);

            if (!existingUser.SignInSecret.Equals(request.Code))
            {
                existingUser.SignInTries -= 1;
                if (!(existingUser.SignInTries <= 0))
                    return Result<TokenDto>.Failure(
                        string.Format(ErrorResults.SignInCodeError, existingUser.SignInTries),
                        ResultStatus.Conflict
                    );

                existingUser.SignInSecret = null;
                return Result<TokenDto>.Failure(ErrorResults.SignInFailed, ResultStatus.Conflict);
            }

            existingUser.SignInSecret = null;

            var token = tokenService.CreateToken(existingUser);
            return Result<TokenDto>.Success(new TokenDto(token));
        }
    }
}

public class LoginEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                Login.Endpoint,
                async (Login.LoginCommand cmd, ISender sender) =>
                {
                    var result = await sender.Send(cmd);
                    return result.ToHttpResult();
                }
            )
            .Produces<TokenDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .WithName("Login")
            .WithTags("User");
    }
}
