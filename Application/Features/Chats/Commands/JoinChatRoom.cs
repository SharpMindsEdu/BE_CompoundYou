using Application.Common;
using Application.Common.Extensions;
using Application.Extensions;
using Application.Features.Chats.DTOs;
using Application.Repositories;
using Carter;
using Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Application.Features.Chats.Commands;

public static class JoinChatRoom
{
    public const string Endpoint = "api/chats/rooms/{roomId:long}/join";

    public record JoinChatRoomCommand(long RoomId, long? UserId)
        : ICommandRequest<Result<ChatRoomDto>>;

    public class Validator : AbstractValidator<JoinChatRoomCommand>
    {
        public Validator()
        {
            RuleFor(x => x.RoomId).GreaterThan(0);
            RuleFor(x => x.UserId).NotNull().GreaterThan(0);
        }
    }

    internal sealed class Handler(
        IRepository<ChatRoom> roomRepo,
        IRepository<ChatRoomUser> userRepo
    ) : IRequestHandler<JoinChatRoomCommand, Result<ChatRoomDto>>
    {
        public async Task<Result<ChatRoomDto>> Handle(
            JoinChatRoomCommand request,
            CancellationToken ct
        )
        {
            var room = await roomRepo.GetById(request.RoomId);
            if (room == null)
                return Result<ChatRoomDto>.Failure(ErrorResults.EntityNotFound, ResultStatus.NotFound);

            if (!room.IsPublic)
                return Result<ChatRoomDto>.Failure(ErrorResults.EntityNotFound, ResultStatus.NotFound);

            var exists = await userRepo.Exist(
                x => x.ChatRoomId == request.RoomId && x.UserId == request.UserId,
                ct
            );
            if (!exists)
            {
                await userRepo.Add(
                    new ChatRoomUser { ChatRoomId = request.RoomId, UserId = request.UserId!.Value }
                );
                await userRepo.SaveChanges(ct);
            }

            return Result<ChatRoomDto>.Success(room);
        }
    }
}

public class JoinChatRoomEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                JoinChatRoom.Endpoint,
                async (long roomId, ISender sender, HttpContext ctx) =>
                {
                    var cmd = new JoinChatRoom.JoinChatRoomCommand(roomId, ctx.GetUserId());
                    var result = await sender.Send(cmd);
                    return result.ToHttpResult();
                }
            )
            .RequireAuthorization()
            .Produces<ChatRoomDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .WithTags("Chat");
    }
}
