using Application.Extensions;
using Application.Features.Riftbound.DeckOptimization.DTOs;
using Application.Features.Riftbound.DeckOptimization.Services;
using Application.Shared;
using Application.Shared.Extensions;
using Carter;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Application.Features.Riftbound.DeckOptimization.Queries;

public static class GetRiftboundDeckOptimizationRun
{
    public const string Endpoint = "api/riftbound/ai/deck-optimization-runs/{runId:long}";

    public record Query(long RunId, long? UserId) : IRequest<Result<RiftboundDeckOptimizationRunDto>>;

    public class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(x => x.RunId).GreaterThan(0);
            RuleFor(x => x.UserId).NotNull().GreaterThan(0);
        }
    }

    internal sealed class Handler(IRiftboundDeckOptimizationService optimizationService)
        : IRequestHandler<Query, Result<RiftboundDeckOptimizationRunDto>>
    {
        public Task<Result<RiftboundDeckOptimizationRunDto>> Handle(
            Query request,
            CancellationToken cancellationToken
        )
        {
            return optimizationService.GetRunAsync(
                request.UserId!.Value,
                request.RunId,
                cancellationToken
            );
        }
    }
}

public sealed class GetRiftboundDeckOptimizationRunEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                GetRiftboundDeckOptimizationRun.Endpoint,
                async (long runId, ISender sender, HttpContext httpContext) =>
                {
                    var query = new GetRiftboundDeckOptimizationRun.Query(
                        runId,
                        httpContext.GetUserId()
                    );
                    var result = await sender.Send(query);
                    return result.ToHttpResult();
                }
            )
            .RequireAuthorization()
            .Produces<RiftboundDeckOptimizationRunDto>()
            .Produces(StatusCodes.Status404NotFound)
            .WithName("GetRiftboundDeckOptimizationRun")
            .WithTags("Riftbound AI");
    }
}
