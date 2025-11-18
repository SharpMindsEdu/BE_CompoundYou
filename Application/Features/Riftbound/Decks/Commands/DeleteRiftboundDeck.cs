using Application.Common;
using Application.Common.Extensions;
using Application.Extensions;
using Application.Repositories;
using Carter;
using Domain.Entities.Riftbound;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Application.Features.Riftbound.Decks.Commands;

public static class DeleteRiftboundDeck
{
    public const string Endpoint = "api/riftbound/decks/{deckId:long}";

    public record DeleteRiftboundDeckCommand(long DeckId, long? UserId)
        : ICommandRequest<Result<bool>>;

    public class Validator : AbstractValidator<DeleteRiftboundDeckCommand>
    {
        public Validator()
        {
            RuleFor(x => x.DeckId).GreaterThan(0);
            RuleFor(x => x.UserId).NotNull().GreaterThan(0);
        }
    }

    internal sealed class Handler(IRepository<RiftboundDeck> deckRepository)
        : IRequestHandler<DeleteRiftboundDeckCommand, Result<bool>>
    {
        public async Task<Result<bool>> Handle(DeleteRiftboundDeckCommand request, CancellationToken ct)
        {
            var deck = await deckRepository.GetById(request.DeckId);
            if (deck is null)
            {
                return Result<bool>.Failure(ErrorResults.EntityNotFound, ResultStatus.NotFound);
            }

            if (deck.OwnerId != request.UserId)
            {
                return Result<bool>.Failure(ErrorResults.Forbidden, ResultStatus.Forbidden);
            }

            deckRepository.Remove(deck);
            await deckRepository.SaveChanges(ct);
            return Result<bool>.Success(true);
        }
    }
}

public class DeleteRiftboundDeckEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete(
                DeleteRiftboundDeck.Endpoint,
                async (
                    long deckId,
                    ISender sender,
                    HttpContext httpContext
                ) =>
                {
                    var command = new DeleteRiftboundDeck.DeleteRiftboundDeckCommand(
                        deckId,
                        httpContext.GetUserId()
                    );
                    var result = await sender.Send(command);
                    return result.ToHttpResult();
                }
            )
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("DeleteRiftboundDeck")
            .WithTags("Riftbound Decks");
    }
}
