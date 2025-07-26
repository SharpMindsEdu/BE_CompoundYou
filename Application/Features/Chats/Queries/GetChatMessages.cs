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
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Application.Features.Chats.Queries;

public static class GetChatMessages
{
    public const string Endpoint = "api/chats/rooms/{roomId:long}/messages";

    public record GetChatMessagesQuery(
        [FromRoute] long RoomId,
        long? UserId,
        [FromQuery] int Page = 1,
        [FromQuery] int PageSize = 50
    ) : IRequest<Result<Page<ChatMessageDto>>>;

    public class Validator : AbstractValidator<GetChatMessagesQuery>
    {
        public Validator()
        {
            RuleFor(x => x.RoomId).GreaterThan(0);
            RuleFor(x => x.UserId).NotNull().GreaterThan(0);
            RuleFor(x => x.Page).GreaterThan(0);
            RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        }
    }

    internal sealed class Handler(
        IRepository<ChatMessage> messageRepo,
        IRepository<ChatRoomUser> userRepo
    ) : IRequestHandler<GetChatMessagesQuery, Result<Page<ChatMessageDto>>>
    {
        public async Task<Result<Page<ChatMessageDto>>> Handle(
            GetChatMessagesQuery request,
            CancellationToken ct
        )
        {
            var isMember = await userRepo.Exist(
                x => x.ChatRoomId == request.RoomId && x.UserId == request.UserId,
                ct
            );
            if (!isMember)
                return Result<Page<ChatMessageDto>>.Failure(
                    ErrorResults.EntityNotFound,
                    ResultStatus.NotFound
                );

            var page = await messageRepo.ListAllPaged(
                x => x.ChatRoomId == request.RoomId,
                request.Page,
                request.PageSize,
                ct
            );

            return Result<Page<ChatMessageDto>>.Success(page);
        }
    }
}

public class GetChatMessagesEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                GetChatMessages.Endpoint,
                async (
                    [AsParameters] GetChatMessages.GetChatMessagesQuery query,
                    ISender sender,
                    HttpContext ctx
                ) =>
                {
                    var enriched = query with { UserId = ctx.GetUserId() };
                    var result = await sender.Send(enriched);
                    return result.ToHttpResult();
                }
            )
            .RequireAuthorization()
            .Produces<Page<ChatMessageDto>>()
            .Produces(StatusCodes.Status400BadRequest)
            .WithTags("Chat");
    }
}
