using Application.Extensions;
using Application.Features.Riftbound.Decks.DTOs;
using Application.Features.Riftbound.Simulation.Services;
using Application.Shared;
using Application.Shared.Extensions;
using Carter;
using Domain.Entities.Riftbound;
using Domain.Repositories;
using Domain.Specifications.Riftbound.Decks;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Application.Features.Riftbound.Decks.Commands;

public static class CreateRiftboundDeck
{
    public const string Endpoint = "api/riftbound/decks";

    public record CreateRiftboundDeckCommand(
        long? UserId,
        string Name,
        long LegendId,
        long ChampionId,
        bool IsPublic,
        IReadOnlyCollection<RiftboundDeckCardInput> Cards,
        IReadOnlyCollection<RiftboundDeckSideboardCardInput>? SideboardCards,
        IReadOnlyCollection<RiftboundDeckRuneInput>? RuneCards,
        IReadOnlyCollection<long>? BattlefieldCardIds,
        IReadOnlyCollection<long>? SharedWithUserIds
    ) : ICommandRequest<Result<RiftboundDeckDto>>;

    public class Validator : AbstractValidator<CreateRiftboundDeckCommand>
    {
        public Validator()
        {
            RuleFor(x => x.UserId).NotNull().GreaterThan(0);
            RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
            RuleFor(x => x.LegendId).GreaterThan(0);
            RuleFor(x => x.ChampionId).GreaterThan(0);
            RuleFor(x => x.Cards)
                .NotNull()
                .Must(x => x.Count > 0)
                .WithMessage("Deck benötigt Karten.");
            RuleForEach(x => x.Cards)
                .ChildRules(card =>
                {
                    card.RuleFor(c => c.CardId).GreaterThan(0);
                    card.RuleFor(c => c.Quantity).GreaterThan(0);
                });
            RuleForEach(x => x.SideboardCards)
                .ChildRules(card =>
                {
                    card.RuleFor(c => c.CardId).GreaterThan(0);
                    card.RuleFor(c => c.Quantity).GreaterThan(0);
                });
            RuleForEach(x => x.RuneCards)
                .ChildRules(card =>
                {
                    card.RuleFor(c => c.CardId).GreaterThan(0);
                    card.RuleFor(c => c.Quantity).GreaterThan(0);
                });
            RuleForEach(x => x.BattlefieldCardIds).GreaterThan(0);
        }
    }

    internal sealed class Handler(
        IRepository<RiftboundDeck> deckRepository,
        IRepository<RiftboundCard> cardRepository,
        IRiftboundDeckSimulationReadinessService readinessService,
        IRiftboundDeckSpecification deckSpecification
    ) : IRequestHandler<CreateRiftboundDeckCommand, Result<RiftboundDeckDto>>
    {
        public async Task<Result<RiftboundDeckDto>> Handle(
            CreateRiftboundDeckCommand request,
            CancellationToken ct
        )
        {
            var ownerId = request.UserId!.Value;
            var validation = await RiftboundDeckCommandHelper.ValidateDeckAsync(
                request.LegendId,
                request.ChampionId,
                request.Cards,
                request.SideboardCards,
                request.RuneCards,
                request.BattlefieldCardIds,
                cardRepository,
                ct
            );
            if (!validation.Succeeded)
            {
                return Result<RiftboundDeckDto>.Failure(
                    validation.ErrorMessage!,
                    validation.Status
                );
            }

            var deck = new RiftboundDeck
            {
                Name = request.Name.Trim(),
                LegendId = validation.Data!.Legend.Id,
                ChampionId = validation.Data.Champion.Id,
                OwnerId = ownerId,
                IsPublic = request.IsPublic,
                Colors = validation.Data.Colors,
                Cards = validation.Data.Cards,
                SideboardCards = validation.Data.SideboardCards,
                Runes = validation.Data.Runes,
                Battlefields = validation.Data.Battlefields,
                Shares = RiftboundDeckCommandHelper.BuildShares(request.SharedWithUserIds, ownerId),
            };

            await deckRepository.Add(deck);
            await deckRepository.SaveChanges(ct);

            var dto = await RiftboundDeckCommandHelper.LoadDeckDtoAsync(
                deckSpecification,
                deck.Id,
                ownerId,
                readinessService,
                ct
            );
            return Result<RiftboundDeckDto>.Success(dto);
        }
    }
}

public class CreateRiftboundDeckEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                CreateRiftboundDeck.Endpoint,
                async (
                    CreateRiftboundDeck.CreateRiftboundDeckCommand command,
                    ISender sender,
                    HttpContext httpContext
                ) =>
                {
                    var enriched = command with { UserId = httpContext.GetUserId() };
                    var result = await sender.Send(enriched);
                    return result.ToHttpResult();
                }
            )
            .RequireAuthorization()
            .Produces<RiftboundDeckDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .WithName("CreateRiftboundDeck")
            .WithTags("Riftbound Decks");
    }
}
