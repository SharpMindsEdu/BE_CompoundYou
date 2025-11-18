using System.Linq;
using Application.Extensions;
using Application.Features.Chats.DTOs;
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

public static class CreateChatRoom
{
    public const string Endpoint = "api/chats/rooms";

    public record CreateChatRoomCommand(
        long? UserId,
        string Name,
        bool IsPublic,
        List<long>? UserIds
    ) : ICommandRequest<Result<ChatRoomDto>>;

    public class Validator : AbstractValidator<CreateChatRoomCommand>
    {
        public Validator()
        {
            RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        }
    }

    internal sealed class Handler(IRepository<ChatRoom> repo, IRepository<ChatRoomUser> userRepo)
        : IRequestHandler<CreateChatRoomCommand, Result<ChatRoomDto>>
    {
        public async Task<Result<ChatRoomDto>> Handle(
            CreateChatRoomCommand request,
            CancellationToken ct
        )
        {
            var room = new ChatRoom
            {
                Name = request.Name,
                IsPublic = request.IsPublic,
                IsDirect = request.UserIds?.Count == 2 && !request.IsPublic,
            };
            await repo.Add(room);
            await repo.SaveChanges(ct);

            var ids = request.UserIds ?? [];
            foreach (var id in ids.Distinct())
            {
                await userRepo.Add(
                    new ChatRoomUser
                    {
                        ChatRoomId = room.Id,
                        UserId = id,
                        IsAdmin = false,
                    }
                );
            }
            if (request.UserId.HasValue)
            {
                await userRepo.Add(
                    new ChatRoomUser
                    {
                        ChatRoomId = room.Id,
                        UserId = request.UserId.Value,
                        IsAdmin = true,
                    }
                );
            }
            await userRepo.SaveChanges(ct);

            return Result<ChatRoomDto>.Success(room);
        }
    }
}

public class CreateChatRoomEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                CreateChatRoom.Endpoint,
                async (CreateChatRoom.CreateChatRoomCommand cmd, ISender sender, HttpContext ctx) =>
                {
                    var enriched = cmd with
                    {
                        UserId = ctx.GetUserId(),
                        UserIds = cmd.UserIds ?? [],
                    };
                    var result = await sender.Send(enriched);
                    return result.ToHttpResult();
                }
            )
            .RequireAuthorization()
            .Produces<ChatRoomDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .WithTags("Chat");
    }
}
