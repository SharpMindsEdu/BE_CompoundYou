using Application.Common;
using Application.Common.Extensions;
using Application.Features.Users.DTOs;
using Application.Repositories;
using Carter;
using Domain;
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
        : IRequest<Result<UserDto>>;

    private class Validator : AbstractValidator<RegisterUserCommand>
    {
        public Validator()
        {
            RuleFor(x => x.DisplayName).NotEmpty();
            RuleFor(x => x.Email)
                .Must(
                    (cmd, email) =>
                        !string.IsNullOrEmpty(email) || !string.IsNullOrEmpty(cmd.PhoneNumber)
                )
                .WithMessage("Email or PhoneNumber must be provided.");
        }
    }

    internal sealed class Handler(IRepository<User> repo) : IRequestHandler<RegisterUserCommand, Result<UserDto>>
    {
        public async Task<Result<UserDto>> Handle(RegisterUserCommand request, CancellationToken ct)
        {
            if (request.Email is not null)
            {
                var emailExists = await repo.Exist(x => x.Email == request.Email, ct);
                if (emailExists)
                {
                    return Result<UserDto>.Failure("Email already in use.", ResultStatus.Conflict);
                }
            }

            if (request.PhoneNumber is not null)
            {
                var phoneExists = await repo.Exist(x => x.PhoneNumber == request.PhoneNumber, ct);
                if (phoneExists)
                {
                    return Result<UserDto>.Failure(
                        "PhoneNumber already in use.",
                        ResultStatus.Conflict
                    );
                }
            }
            var user = new User
            {
                DisplayName = request.DisplayName,
                Email = request.Email,
                PhoneNumber = request.PhoneNumber,
            };
            await repo.Add(user);
            await repo.SaveChanges(ct);
            return Result<UserDto>.Success(user);
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
            .Produces<UserDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .WithName("RegisterUser")
            .WithTags("Users");
    }
}
