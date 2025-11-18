using Application.Common;
using Application.Common.Extensions;
using Application.Features.Riftbound.Cards.Specifications;
using Carter;
using Domain.Entities.Riftbound;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Application.Features.Riftbound.Cards.Queries;

public static class GetRiftboundCards
{
    public const string Endpoint = "api/riftbound/cards";

    public record GetRiftboundCardsQuery(
        int? MinCost,
        int? MaxCost,
        string? Type,
        int? MinMight,
        int? MaxMight,
        string? SetName,
        string? Rarity,
        List<string>? Colors,
        string? Name
    ) : IRequest<Result<List<RiftboundCardResponse>>>;

    public class Validator : AbstractValidator<GetRiftboundCardsQuery>
    {
        public Validator()
        {
            RuleFor(x => x.MinCost).GreaterThanOrEqualTo(0).When(x => x.MinCost.HasValue);
            RuleFor(x => x.MaxCost).GreaterThanOrEqualTo(0).When(x => x.MaxCost.HasValue);
            RuleFor(x => x.MinMight).GreaterThanOrEqualTo(0).When(x => x.MinMight.HasValue);
            RuleFor(x => x.MaxMight).GreaterThanOrEqualTo(0).When(x => x.MaxMight.HasValue);
            RuleFor(x => x)
                .Must(x => !x.MinCost.HasValue || !x.MaxCost.HasValue || x.MinCost <= x.MaxCost)
                .WithMessage("MinCost darf MaxCost nicht überschreiten.");
            RuleFor(x => x)
                .Must(x => !x.MinMight.HasValue || !x.MaxMight.HasValue || x.MinMight <= x.MaxMight)
                .WithMessage("MinMight darf MaxMight nicht überschreiten.");
        }
    }

    public record RiftboundCardResponse(
        long Id,
        string ReferenceId,
        string Name,
        string? Effect,
        IReadOnlyCollection<string>? Color,
        int? Cost,
        string? Type,
        int? Might,
        IReadOnlyCollection<string>? Tags,
        string? SetName,
        string? Rarity,
        string? Cycle,
        string? Image,
        bool Promo
    );

    internal sealed class Handler(IRiftboundCardSpecification specification)
        : IRequestHandler<GetRiftboundCardsQuery, Result<List<RiftboundCardResponse>>>
    {
        public async Task<Result<List<RiftboundCardResponse>>> Handle(
            GetRiftboundCardsQuery request,
            CancellationToken ct
        )
        {
            var cards = await specification
                .ByFilter(
                    request.MinCost,
                    request.MaxCost,
                    request.Type,
                    request.MinMight,
                    request.MaxMight,
                    request.SetName,
                    request.Rarity,
                    request.Colors,
                    request.Name
                )
                .ToList(ct);

            var response = cards
                .Select(card =>
                    new RiftboundCardResponse(
                        card.Id,
                        card.ReferenceId,
                        card.Name,
                        card.Effect,
                        card.Color,
                        card.Cost,
                        card.Type,
                        card.Might,
                        card.Tags,
                        card.SetName,
                        card.Rarity,
                        card.Cycle,
                        card.Image,
                        card.Promo
                    )
                )
                .ToList();

            return Result<List<RiftboundCardResponse>>.Success(response);
        }
    }
}

public class GetRiftboundCardsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                GetRiftboundCards.Endpoint,
                async (
                    [AsParameters] GetRiftboundCards.GetRiftboundCardsQuery query,
                    ISender sender
                ) =>
                {
                    var result = await sender.Send(query);
                    return result.ToHttpResult();
                }
            )
            .Produces<List<GetRiftboundCards.RiftboundCardResponse>>()
            .Produces(StatusCodes.Status400BadRequest)
            .WithName("GetRiftboundCards")
            .WithTags("Riftbound Cards");
    }
}
