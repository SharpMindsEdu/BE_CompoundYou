using Application.Extensions;
using Application.Features.Riftbound.Simulation.DTOs;
using Application.Features.Riftbound.Simulation.Services;
using Application.Shared;
using Application.Shared.Extensions;
using Carter;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Application.Features.Riftbound.Simulation.Queries;

public static class GetRiftboundDeckSimulationSupport
{
    public const string Endpoint = "api/riftbound/decks/{deckId:long}/simulation-support";

    public record Query(long DeckId, long? UserId) : IRequest<Result<RiftboundDeckSimulationSupportDto>>;

    public class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(x => x.DeckId).GreaterThan(0);
            RuleFor(x => x.UserId).NotNull().GreaterThan(0);
        }
    }

    internal sealed class Handler(IRiftboundSimulationService simulationService)
        : IRequestHandler<Query, Result<RiftboundDeckSimulationSupportDto>>
    {
        public Task<Result<RiftboundDeckSimulationSupportDto>> Handle(
            Query request,
            CancellationToken cancellationToken
        )
        {
            return simulationService.GetDeckSimulationSupportAsync(
                request.UserId!.Value,
                request.DeckId,
                cancellationToken
            );
        }
    }
}

public sealed class GetRiftboundDeckSimulationSupportEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                GetRiftboundDeckSimulationSupport.Endpoint,
                async (long deckId, ISender sender, HttpContext httpContext) =>
                {
                    var query = new GetRiftboundDeckSimulationSupport.Query(
                        deckId,
                        httpContext.GetUserId()
                    );
                    var result = await sender.Send(query);
                    return result.ToHttpResult();
                }
            )
            .RequireAuthorization()
            .Produces<RiftboundDeckSimulationSupportDto>()
            .Produces(StatusCodes.Status404NotFound)
            .WithName("GetRiftboundDeckSimulationSupport")
            .WithTags("Riftbound Simulation");
    }
}
