using Application.Common;
using Application.Common.Extensions;
using Application.Extensions;
using Application.Repositories;
using Carter;
using Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Application.Features.Users.Commands;

public static class BlockUser
{
    public const string Endpoint = "api/users/{userId:long}/block";

    public record BlockUserCommand(long UserId, long? RequestingUserId) : ICommandRequest<Result<bool>>;

    public class Validator : AbstractValidator<BlockUserCommand>
    {
        public Validator()
        {
            RuleFor(x => x.UserId).GreaterThan(0);
            RuleFor(x => x.RequestingUserId).NotNull().GreaterThan(0);
        }
    }

    internal sealed class Handler(IRepository<UserBlock> repo)
        : IRequestHandler<BlockUserCommand, Result<bool>>
    {
        public async Task<Result<bool>> Handle(BlockUserCommand request, CancellationToken ct)
        {
            var exists = await repo.Exist(
                x => x.UserId == request.RequestingUserId && x.BlockedUserId == request.UserId,
                ct
            );
            if (!exists)
            {
                await repo.Add(new UserBlock { UserId = request.RequestingUserId!.Value, BlockedUserId = request.UserId });
                await repo.SaveChanges(ct);
            }

            return Result<bool>.Success(true);
        }
    }
}

public class BlockUserEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                BlockUser.Endpoint,
                async (long userId, ISender sender, HttpContext ctx) =>
                {
                    var cmd = new BlockUser.BlockUserCommand(userId, ctx.GetUserId());
                    var result = await sender.Send(cmd);
                    return result.ToHttpResult();
                }
            )
            .RequireAuthorization()
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .WithTags("User");
    }
}
