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

public static class UpdateRiftboundDeck
{
    public const string Endpoint = "api/riftbound/decks/{deckId:long}";

    public record UpdateRiftboundDeckCommand(
        long DeckId,
        long? UserId,
        string Name,
        long LegendId,
        long ChampionId,
        bool IsPublic,
        IReadOnlyCollection<RiftboundDeckCardInput> Cards,
        IReadOnlyCollection<long>? SharedWithUserIds
    ) : ICommandRequest<Result<RiftboundDeckDto>>;

    public class Validator : AbstractValidator<UpdateRiftboundDeckCommand>
    {
        public Validator()
        {
            RuleFor(x => x.DeckId).GreaterThan(0);
            RuleFor(x => x.UserId).NotNull().GreaterThan(0);
            RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
            RuleFor(x => x.LegendId).GreaterThan(0);
            RuleFor(x => x.ChampionId).GreaterThan(0);
            RuleFor(x => x.Cards).NotNull().Must(x => x.Count > 0);
            RuleForEach(x => x.Cards)
                .ChildRules(card =>
                {
                    card.RuleFor(c => c.CardId).GreaterThan(0);
                    card.RuleFor(c => c.Quantity).GreaterThan(0);
                });
        }
    }

    internal sealed class Handler(
        IRepository<RiftboundDeck> deckRepository,
        IRepository<RiftboundDeckCard> deckCardRepository,
        IRepository<RiftboundDeckShare> deckShareRepository,
        IRepository<RiftboundCard> cardRepository,
        IRiftboundDeckSpecification deckSpecification
    ) : IRequestHandler<UpdateRiftboundDeckCommand, Result<RiftboundDeckDto>>
    {
        public async Task<Result<RiftboundDeckDto>> Handle(
            UpdateRiftboundDeckCommand request,
            CancellationToken ct
        )
        {
            var deck = await deckRepository.GetById(request.DeckId);
            if (deck is null)
            {
                return Result<RiftboundDeckDto>.Failure(
                    ErrorResults.EntityNotFound,
                    ResultStatus.NotFound
                );
            }

            if (deck.OwnerId != request.UserId)
            {
                return Result<RiftboundDeckDto>.Failure(
                    ErrorResults.Forbidden,
                    ResultStatus.Forbidden
                );
            }

            var validation = await RiftboundDeckCommandHelper.ValidateDeckAsync(
                request.LegendId,
                request.ChampionId,
                request.Cards,
                cardRepository,
                ct
            );

            if (!validation.Succeeded)
            {
                return Result<RiftboundDeckDto>.Failure(validation.ErrorMessage!, validation.Status);
            }

            deck.Name = request.Name.Trim();
            deck.LegendId = validation.Data!.Legend.Id;
            deck.ChampionId = validation.Data.Champion.Id;
            deck.Colors = validation.Data.Colors;
            deck.IsPublic = request.IsPublic;

            await deckCardRepository.Remove(x => x.DeckId == deck.Id, ct);
            var newCards = validation.Data.Cards
                .Select(c => new RiftboundDeckCard
                {
                    DeckId = deck.Id,
                    CardId = c.CardId,
                    Quantity = c.Quantity,
                })
                .ToArray();
            await deckCardRepository.Add(newCards);

            await deckShareRepository.Remove(x => x.DeckId == deck.Id, ct);
            var shares = RiftboundDeckCommandHelper
                .BuildShares(request.SharedWithUserIds, deck.OwnerId)
                .Select(share =>
                {
                    share.DeckId = deck.Id;
                    return share;
                })
                .ToArray();
            if (shares.Length > 0)
            {
                await deckShareRepository.Add(shares);
            }

            deckRepository.Update(deck);
            await deckRepository.SaveChanges(ct);

            var dto = await RiftboundDeckCommandHelper.LoadDeckDtoAsync(
                deckSpecification,
                deck.Id,
                deck.OwnerId,
                ct
            );
            return Result<RiftboundDeckDto>.Success(dto);
        }
    }
}

public class UpdateRiftboundDeckEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
                UpdateRiftboundDeck.Endpoint,
                async (
                    long deckId,
                    UpdateRiftboundDeck.UpdateRiftboundDeckCommand command,
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
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("UpdateRiftboundDeck")
            .WithTags("Riftbound Decks");
    }
}
