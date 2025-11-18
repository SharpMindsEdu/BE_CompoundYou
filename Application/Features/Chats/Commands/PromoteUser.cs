using Application.Extensions;
using Application.Shared;
using Application.Shared.Extensions;
using Carter;
using Domain.Entities;
using Domain.Entities.Chat;
using Domain.Repositories;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Application.Features.Chats.Commands;

public static class PromoteUser
{
    public const string Endpoint = "api/chats/rooms/{roomId:long}/promote/{userId:long}";

    public record PromoteUserCommand(long RoomId, long UserId, long? RequestingUserId)
        : ICommandRequest<Result<bool>>;

    public class Validator : AbstractValidator<PromoteUserCommand>
    {
        public Validator()
        {
            RuleFor(x => x.RoomId).GreaterThan(0);
            RuleFor(x => x.UserId).GreaterThan(0);
            RuleFor(x => x.RequestingUserId).NotNull().GreaterThan(0);
        }
    }

    internal sealed class Handler(IRepository<ChatRoomUser> repo)
        : IRequestHandler<PromoteUserCommand, Result<bool>>
    {
        public async Task<Result<bool>> Handle(PromoteUserCommand request, CancellationToken ct)
        {
            var admin = await repo.GetByExpression(x =>
                x.ChatRoomId == request.RoomId && x.UserId == request.RequestingUserId
            );
            if (admin is null || !admin.IsAdmin)
                return Result<bool>.Failure(ErrorResults.Forbidden, ResultStatus.Forbidden);

            var user = await repo.GetByExpression(x =>
                x.ChatRoomId == request.RoomId && x.UserId == request.UserId
            );
            if (user is null)
                return Result<bool>.Failure(ErrorResults.EntityNotFound, ResultStatus.NotFound);

            user.IsAdmin = true;
            repo.Update(user);
            await repo.SaveChanges(ct);

            return Result<bool>.Success(true);
        }
    }
}

public class PromoteUserEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                PromoteUser.Endpoint,
                async (long roomId, long userId, ISender sender, HttpContext ctx) =>
                {
                    var cmd = new PromoteUser.PromoteUserCommand(roomId, userId, ctx.GetUserId());
                    var result = await sender.Send(cmd);
                    return result.ToHttpResult();
                }
            )
            .RequireAuthorization()
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status403Forbidden)
            .WithTags("Chat");
    }
}
