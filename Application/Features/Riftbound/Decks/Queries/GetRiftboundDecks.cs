using Application.Common;
using Application.Common.Extensions;
using Application.Extensions;
using Application.Features.Riftbound.Decks.DTOs;
using Application.Features.Riftbound.Decks.Specifications;
using Application.Repositories;
using Carter;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Application.Features.Riftbound.Decks.Queries;

public static class GetRiftboundDecks
{
    public const string Endpoint = "api/riftbound/decks";

    public record GetRiftboundDecksQuery(
        long? UserId,
        List<long>? LegendIds,
        List<string>? Colors,
        int Page = 1,
        int PageSize = 20
    ) : IRequest<Result<Page<RiftboundDeckDto>>>;

    public class Validator : AbstractValidator<GetRiftboundDecksQuery>
    {
        public Validator()
        {
            RuleFor(x => x.UserId).NotNull().GreaterThan(0);
            RuleFor(x => x.Page).GreaterThan(0);
            RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        }
    }

    internal sealed class Handler(IRiftboundDeckSpecification deckSpecification)
        : IRequestHandler<GetRiftboundDecksQuery, Result<Page<RiftboundDeckDto>>>
    {
        public async Task<Result<Page<RiftboundDeckDto>>> Handle(
            GetRiftboundDecksQuery request,
            CancellationToken ct
        )
        {
            var spec = deckSpecification
                .Reset()
                .IncludeDetails()
                .AccessibleForUser(request.UserId!.Value)
                .FilterBy(request.LegendIds, NormalizeColors(request.Colors))
                .OrderByNewest();

            var page = await spec.ToPage(request.Page, request.PageSize, ct);
            var dtoItems = page.Items
                .Select(deck => RiftboundDeckMappings.ToDto(deck, request.UserId.Value))
                .ToList();

            var dtoPage = new Page<RiftboundDeckDto>(
                page.CurrentPage,
                page.NextPage,
                page.TotalPages,
                page.PageSize,
                page.TotalItems,
                dtoItems
            );

            return Result<Page<RiftboundDeckDto>>.Success(dtoPage);
        }

        private static IReadOnlyCollection<string>? NormalizeColors(
            List<string>? colors
        )
        {
            return colors?.Select(c => c.Trim()).Where(c => !string.IsNullOrWhiteSpace(c)).ToList();
        }
    }
}

public class GetRiftboundDecksEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                GetRiftboundDecks.Endpoint,
                async (
                    [AsParameters] GetRiftboundDecks.GetRiftboundDecksQuery query,
                    ISender sender,
                    HttpContext httpContext
                ) =>
                {
                    var enriched = query with { UserId = httpContext.GetUserId() };
                    var result = await sender.Send(enriched);
                    return result.ToHttpResult();
                }
            )
            .RequireAuthorization()
            .Produces<Page<RiftboundDeckDto>>()
            .Produces(StatusCodes.Status400BadRequest)
            .WithName("GetRiftboundDecks")
            .WithTags("Riftbound Decks");
    }
}
