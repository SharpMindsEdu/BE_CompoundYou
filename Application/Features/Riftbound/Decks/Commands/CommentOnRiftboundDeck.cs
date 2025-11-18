using Application.Common;
using Application.Common.Extensions;
using Application.Extensions;
using Application.Features.Riftbound.Decks.DTOs;
using Application.Features.Riftbound.Decks.Specifications;
using Application.Repositories;
using Carter;
using Domain.Entities;
using Domain.Entities.Riftbound;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Application.Features.Riftbound.Decks.Commands;

public static class CommentOnRiftboundDeck
{
    public const string Endpoint = "api/riftbound/decks/{deckId:long}/comments";

    public record CommentOnRiftboundDeckCommand(
        long DeckId,
        long? UserId,
        string Content,
        long? ParentCommentId
    ) : ICommandRequest<Result<RiftboundDeckCommentDto>>;

    public class Validator : AbstractValidator<CommentOnRiftboundDeckCommand>
    {
        public Validator()
        {
            RuleFor(x => x.DeckId).GreaterThan(0);
            RuleFor(x => x.UserId).NotNull().GreaterThan(0);
            RuleFor(x => x.Content).NotEmpty().MaximumLength(2000);
            RuleFor(x => x.ParentCommentId).GreaterThan(0).When(x => x.ParentCommentId.HasValue);
        }
    }

    internal sealed class Handler(
        IRepository<RiftboundDeckComment> commentRepository,
        IRepository<User> userRepository,
        IRiftboundDeckSpecification deckSpecification
    ) : IRequestHandler<CommentOnRiftboundDeckCommand, Result<RiftboundDeckCommentDto>>
    {
        public async Task<Result<RiftboundDeckCommentDto>> Handle(
            CommentOnRiftboundDeckCommand request,
            CancellationToken ct
        )
        {
            var userId = request.UserId!.Value;
            var deck = await deckSpecification
                .Reset()
                .AccessibleForUser(userId)
                .ByDeckId(request.DeckId)
                .FirstOrDefault(ct);

            if (deck is null)
            {
                return Result<RiftboundDeckCommentDto>.Failure(
                    ErrorResults.DeckAccessDenied,
                    ResultStatus.NotFound
                );
            }

            if (request.ParentCommentId.HasValue)
            {
                var parent = await commentRepository.GetById(request.ParentCommentId.Value);
                if (parent is null || parent.DeckId != request.DeckId)
                {
                    return Result<RiftboundDeckCommentDto>.Failure(
                        ErrorResults.DeckCommentNotFound,
                        ResultStatus.NotFound
                    );
                }
            }

            var comment = new RiftboundDeckComment
            {
                DeckId = request.DeckId,
                UserId = userId,
                ParentCommentId = request.ParentCommentId,
                Content = request.Content.Trim(),
            };

            await commentRepository.Add(comment);
            await commentRepository.SaveChanges(ct);

            var author = await userRepository.GetById(userId);

            var dto = new RiftboundDeckCommentDto(
                comment.Id,
                userId,
                author?.DisplayName ?? string.Empty,
                comment.Content,
                comment.ParentCommentId,
                comment.CreatedOn,
                Array.Empty<RiftboundDeckCommentDto>()
            );

            return Result<RiftboundDeckCommentDto>.Success(dto);
        }
    }
}

public class CommentOnRiftboundDeckEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                CommentOnRiftboundDeck.Endpoint,
                async (
                    long deckId,
                    CommentOnRiftboundDeck.CommentOnRiftboundDeckCommand command,
                    ISender sender,
                    HttpContext httpContext
                ) =>
                {
                    var enriched = command with
                    {
                        DeckId = deckId,
                        UserId = httpContext.GetUserId(),
                    };
                    var result = await sender.Send(enriched);
                    return result.ToHttpResult();
                }
            )
            .RequireAuthorization()
            .Produces<RiftboundDeckCommentDto>()
            .Produces(StatusCodes.Status404NotFound)
            .WithName("CommentOnRiftboundDeck")
            .WithTags("Riftbound Decks");
    }
}
