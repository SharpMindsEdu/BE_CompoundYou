using Application.Common;
using Application.Common.Extensions;
using Application.Repositories;
using Carter;
using Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Application.Features.Users.Commands;

/// <summary>
/// Checks for existing email or phone number and sets a login validation code
/// Upon 3 faulty entries a new code will be sent
/// During testing it will be simply 123456
/// </summary>
public static class RequestLogin
{
    public const string Endpoint = "api/users/login-request";

    public record RequestLoginCommand(string? Email, string? PhoneNumber)
        : ICommandRequest<Result<bool>>;

    public class Validator : AbstractValidator<RequestLoginCommand>
    {
        public Validator()
        {
            RuleFor(x => x.Email)
                .Must(
                    (cmd, email) =>
                        !string.IsNullOrEmpty(email) || !string.IsNullOrEmpty(cmd.PhoneNumber)
                )
                .WithMessage(ValidationErrors.EmailAndPhoneNumberMissing);
        }
    }

    internal sealed class Handler(IRepository<User> repo)
        : IRequestHandler<RequestLoginCommand, Result<bool>>
    {
        public async Task<Result<bool>> Handle(RequestLoginCommand request, CancellationToken ct)
        {
            var existingUser = await repo.GetByExpression(
                x =>
                    (request.Email != null && x.Email == request.Email)
                    || (request.PhoneNumber != null && x.PhoneNumber == request.PhoneNumber),
                ct
            );

            if (existingUser == null)
                return Result<bool>.Failure(ErrorResults.EntityNotFound, ResultStatus.NotFound);

            if (existingUser.SignInSecret is null)
            {
                return Result<bool>.Failure(ErrorResults.SignInNotFound, ResultStatus.Conflict);
            }

            existingUser.SignInSecret = GenerateRandomCode();
            existingUser.SignInTries = 3;
            repo.Update(existingUser);
            return Result<bool>.Success(true);
        }
    }

    private static string GenerateRandomCode()
    {
        var code = "";
        for (var i = 0; i < 6; i++)
        {
            code += Random.Shared.Next(0, 10).ToString();
        }

        return "123456";
    }
}

public class RequestLoginEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                RequestLogin.Endpoint,
                async (RequestLogin.RequestLoginCommand cmd, ISender sender) =>
                {
                    var result = await sender.Send(cmd);
                    return result.ToHttpResult();
                }
            )
            .Produces<bool>()
            .Produces(StatusCodes.Status400BadRequest)
            .WithName("RequestLogin")
            .WithTags("User");
    }
}
