using System.Collections.Generic;
using System.Linq.Expressions;
using Application.Features.Chats.DTOs;
using Application.Shared;
using Application.Shared.Extensions;
using Carter;
using Domain.Entities;
using Domain.Entities.Chat;
using Domain.Repositories;
using FluentValidation;
using Mapster;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Chats.Queries;

public static class GetPublicChatRooms
{
    public const string Endpoint = "api/chats/public";

    public record GetPublicChatRoomsQuery(
        [FromQuery] string? Search,
        [FromQuery] int Page = 1,
        [FromQuery] int PageSize = 50
    ) : IRequest<Result<Page<ChatRoomDto>>>;

    public class Validator : AbstractValidator<GetPublicChatRoomsQuery>
    {
        public Validator()
        {
            RuleFor(x => x.Page).GreaterThan(0);
            RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        }
    }

    internal sealed class Handler(IRepository<ChatRoom> repo)
        : IRequestHandler<GetPublicChatRoomsQuery, Result<Page<ChatRoomDto>>>
    {
        public async Task<Result<Page<ChatRoomDto>>> Handle(
            GetPublicChatRoomsQuery request,
            CancellationToken ct
        )
        {
            Expression<Func<ChatRoom, bool>> predicate = x => x.IsPublic;
            if (!string.IsNullOrEmpty(request.Search))
            {
                var term = request.Search.ToLower();
                predicate = x => x.IsPublic && EF.Functions.Like(x.Name.ToLower(), $"%{term}%");
            }

            var page = await repo.ListAllPaged(predicate, request.Page, request.PageSize, ct);
            var dto = new Page<ChatRoomDto>(
                page.CurrentPage,
                page.NextPage,
                page.TotalPages,
                page.PageSize,
                page.TotalItems,
                page.Items.Adapt<List<ChatRoomDto>>()
            );
            return Result<Page<ChatRoomDto>>.Success(dto);
        }
    }
}

public class GetPublicChatRoomsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                GetPublicChatRooms.Endpoint,
                async (
                    [AsParameters] GetPublicChatRooms.GetPublicChatRoomsQuery query,
                    ISender sender
                ) =>
                {
                    var result = await sender.Send(query);
                    return result.ToHttpResult();
                }
            )
            .RequireAuthorization()
            .Produces<Page<ChatRoomDto>>()
            .Produces(StatusCodes.Status400BadRequest)
            .WithTags("Chat");
    }
}
