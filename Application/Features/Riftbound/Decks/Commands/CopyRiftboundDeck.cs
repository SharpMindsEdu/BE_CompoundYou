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

public static class CopyRiftboundDeck
{
    public const string Endpoint = "api/riftbound/decks/{deckId:long}/copy";

    public record CopyRiftboundDeckCommand(
        long DeckId,
        long? UserId,
        string? Name,
        bool? IsPublic
    ) : ICommandRequest<Result<RiftboundDeckDto>>;

    public class Validator : AbstractValidator<CopyRiftboundDeckCommand>
    {
        public Validator()
        {
            RuleFor(x => x.DeckId).GreaterThan(0);
            RuleFor(x => x.UserId).NotNull().GreaterThan(0);
            RuleFor(x => x.Name).MaximumLength(150);
        }
    }

    internal sealed class Handler(
        IRepository<RiftboundDeck> deckRepository,
        IRiftboundDeckSpecification deckSpecification
    ) : IRequestHandler<CopyRiftboundDeckCommand, Result<RiftboundDeckDto>>
    {
        public async Task<Result<RiftboundDeckDto>> Handle(
            CopyRiftboundDeckCommand request,
            CancellationToken ct
        )
        {
            var sourceDeck = await deckSpecification
                .Reset()
                .IncludeDetails()
                .AccessibleForUser(request.UserId!.Value)
                .ByDeckId(request.DeckId)
                .FirstOrDefault(ct);

            if (sourceDeck is null)
            {
                return Result<RiftboundDeckDto>.Failure(
                    ErrorResults.DeckAccessDenied,
                    ResultStatus.NotFound
                );
            }

            var copyName = string.IsNullOrWhiteSpace(request.Name)
                ? $"Copy of {sourceDeck.Name}"
                : request.Name.Trim();

            var newDeck = new RiftboundDeck
            {
                Name = copyName,
                OwnerId = request.UserId.Value,
                LegendId = sourceDeck.LegendId,
                ChampionId = sourceDeck.ChampionId,
                Colors = sourceDeck.Colors?.ToList() ?? [],
                IsPublic = request.IsPublic ?? false,
                Cards = sourceDeck.Cards
                    .Select(card => new RiftboundDeckCard
                    {
                        CardId = card.CardId,
                        Quantity = card.Quantity,
                    })
                    .ToList(),
            };

            await deckRepository.Add(newDeck);
            await deckRepository.SaveChanges(ct);

            var dto = await RiftboundDeckCommandHelper.LoadDeckDtoAsync(
                deckSpecification,
                newDeck.Id,
                request.UserId.Value,
                ct
            );

            return Result<RiftboundDeckDto>.Success(dto);
        }
    }
}

public class CopyRiftboundDeckEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                CopyRiftboundDeck.Endpoint,
                async (
                    long deckId,
                    CopyRiftboundDeck.CopyRiftboundDeckCommand command,
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
            .WithName("CopyRiftboundDeck")
            .WithTags("Riftbound Decks");
    }
}
