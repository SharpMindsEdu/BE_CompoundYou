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

namespace Application.Features.Chats.Commands;

public static class KickUser
{
    public const string Endpoint = "api/chats/rooms/{roomId:long}/kick/{userId:long}";

    public record KickUserCommand(long RoomId, long UserId, long? RequestingUserId) : ICommandRequest<Result<bool>>;

    public class Validator : AbstractValidator<KickUserCommand>
    {
        public Validator()
        {
            RuleFor(x => x.RoomId).GreaterThan(0);
            RuleFor(x => x.UserId).GreaterThan(0);
            RuleFor(x => x.RequestingUserId).NotNull().GreaterThan(0);
        }
    }

    internal sealed class Handler(IRepository<ChatRoomUser> repo)
        : IRequestHandler<KickUserCommand, Result<bool>>
    {
        public async Task<Result<bool>> Handle(KickUserCommand request, CancellationToken ct)
        {
            var admin = await repo.GetByExpression(x => x.ChatRoomId == request.RoomId && x.UserId == request.RequestingUserId);
            if (admin is null || !admin.IsAdmin)
                return Result<bool>.Failure(ErrorResults.Forbidden, ResultStatus.Forbidden);

            await repo.Remove(x => x.ChatRoomId == request.RoomId && x.UserId == request.UserId, ct);
            await repo.SaveChanges(ct);

            return Result<bool>.Success(true);
        }
    }
}

public class KickUserEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                KickUser.Endpoint,
                async (long roomId, long userId, ISender sender, HttpContext ctx) =>
                {
                    var cmd = new KickUser.KickUserCommand(roomId, userId, ctx.GetUserId());
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
