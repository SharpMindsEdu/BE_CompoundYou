using Application.Common;
using Application.Common.Extensions;
using Application.Features.Users.DTOs;
using Application.Repositories;
using Application.Services;
using Carter;
using Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Application.Features.Users.Commands;

public static class RegisterUser
{
    public const string Endpoint = "api/users/register";

    public record RegisterUserCommand(string DisplayName, string? Email, string? PhoneNumber)
        : ICommandRequest<Result<TokenDto>>;

    public class Validator : AbstractValidator<RegisterUserCommand>
    {
        public Validator()
        {
            RuleFor(x => x.DisplayName).NotEmpty();
            RuleFor(x => x.Email)
                .Must(
                    (cmd, email) =>
                        !string.IsNullOrEmpty(email) || !string.IsNullOrEmpty(cmd.PhoneNumber)
                )
                .WithMessage(ValidationErrors.EmailAndPhoneNumberMissing);
        }
    }

    internal sealed class Handler(IRepository<User> repo, ITokenService tokenService)
        : IRequestHandler<RegisterUserCommand, Result<TokenDto>>
    {
        public async Task<Result<TokenDto>> Handle(
            RegisterUserCommand request,
            CancellationToken ct
        )
        {
            if (request.Email is not null && await repo.Exist(x => x.Email == request.Email, ct))
                return Result<TokenDto>.Failure(ErrorResults.EmailInUse, ResultStatus.Conflict);

            if (
                request.PhoneNumber is not null
                && await repo.Exist(x => x.PhoneNumber == request.PhoneNumber, ct)
            )
                return Result<TokenDto>.Failure(ErrorResults.PhoneInUse, ResultStatus.Conflict);

            var user = new User
            {
                DisplayName = request.DisplayName,
                Email = request.Email,
                PhoneNumber = request.PhoneNumber,
            };

            await repo.Add(user);
            await repo.SaveChanges(ct);

            var token = tokenService.CreateToken(user);
            return Result<TokenDto>.Success(new TokenDto(token));
        }
    }
}

public class RegisterUserEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                RegisterUser.Endpoint,
                async (RegisterUser.RegisterUserCommand cmd, ISender sender) =>
                {
                    var result = await sender.Send(cmd);
                    return result.ToHttpResult();
                }
            )
            .Produces<TokenDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .WithName("RegisterUser")
            .WithTags("User");
    }
}
