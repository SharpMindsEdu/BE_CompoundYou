using Application.Common;
using Application.Common.Extensions;
using Application.Extensions;
using Application.Features.Riftbound.Decks.DTOs;
using Application.Features.Riftbound.Decks.Specifications;
using Application.Repositories;
using Carter;
using Domain.Entities.Riftbound;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Application.Features.Riftbound.Decks.Commands;

public static class RateRiftboundDeck
{
    public const string Endpoint = "api/riftbound/decks/{deckId:long}/rating";

    public record RateRiftboundDeckCommand(long DeckId, int? Rating, long? UserId)
        : ICommandRequest<Result<RiftboundDeckDto>>;

    public class Validator : AbstractValidator<RateRiftboundDeckCommand>
    {
        public Validator()
        {
            RuleFor(x => x.DeckId).GreaterThan(0);
            RuleFor(x => x.UserId).NotNull().GreaterThan(0);
            When(
                x => x.Rating.HasValue,
                () =>
                {
                    RuleFor(x => x.Rating!.Value).InclusiveBetween(1, 5);
                }
            );
        }
    }

    internal sealed class Handler(
        IRepository<RiftboundDeckRating> ratingRepository,
        IRiftboundDeckSpecification deckSpecification
    ) : IRequestHandler<RateRiftboundDeckCommand, Result<RiftboundDeckDto>>
    {
        public async Task<Result<RiftboundDeckDto>> Handle(
            RateRiftboundDeckCommand request,
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
                return Result<RiftboundDeckDto>.Failure(
                    ErrorResults.DeckAccessDenied,
                    ResultStatus.NotFound
                );
            }

            var existingRating = await ratingRepository.GetByExpression(
                r => r.DeckId == request.DeckId && r.UserId == userId,
                ct
            );

            if (request.Rating is null)
            {
                if (existingRating != null)
                {
                    ratingRepository.Remove(existingRating);
                }
            }
            else
            {
                if (existingRating is null)
                {
                    await ratingRepository.Add(
                        new RiftboundDeckRating
                        {
                            DeckId = request.DeckId,
                            UserId = userId,
                            Value = request.Rating.Value,
                        }
                    );
                }
                else
                {
                    existingRating.Value = request.Rating.Value;
                    ratingRepository.Update(existingRating);
                }
            }

            await ratingRepository.SaveChanges(ct);

            var dto = await RiftboundDeckCommandHelper.LoadDeckDtoAsync(
                deckSpecification,
                request.DeckId,
                userId,
                ct
            );
            return Result<RiftboundDeckDto>.Success(dto);
        }
    }
}

public class RateRiftboundDeckEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                RateRiftboundDeck.Endpoint,
                async (
                    long deckId,
                    RateRiftboundDeck.RateRiftboundDeckCommand command,
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
            .Produces<RiftboundDeckDto>()
            .Produces(StatusCodes.Status404NotFound)
            .WithName("RateRiftboundDeck")
            .WithTags("Riftbound Decks");
    }
}
