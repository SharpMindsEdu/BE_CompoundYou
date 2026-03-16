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

namespace Application.Features.Riftbound.DeckOptimization.Commands;

public static class CreateRiftboundDeckOptimizationRun
{
    public const string Endpoint = "api/riftbound/ai/deck-optimization-runs";

    public record Command(
        long? UserId,
        int? PopulationSize,
        int? Generations,
        int? SeedsPerMatch,
        int? MaxAutoplaySteps,
        long? Seed
    ) : ICommandRequest<Result<RiftboundDeckOptimizationRunDto>>;

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.UserId).NotNull().GreaterThan(0);
            RuleFor(x => x.PopulationSize).InclusiveBetween(8, 120).When(x => x.PopulationSize.HasValue);
            RuleFor(x => x.Generations).InclusiveBetween(0, 8).When(x => x.Generations.HasValue);
            RuleFor(x => x.SeedsPerMatch).InclusiveBetween(1, 20).When(x => x.SeedsPerMatch.HasValue);
            RuleFor(x => x.MaxAutoplaySteps)
                .InclusiveBetween(50, 2000)
                .When(x => x.MaxAutoplaySteps.HasValue);
            RuleFor(x => x.Seed).GreaterThan(0).When(x => x.Seed.HasValue);
        }
    }

    internal sealed class Handler(IRiftboundDeckOptimizationService optimizationService)
        : IRequestHandler<Command, Result<RiftboundDeckOptimizationRunDto>>
    {
        public Task<Result<RiftboundDeckOptimizationRunDto>> Handle(
            Command request,
            CancellationToken cancellationToken
        )
        {
            return optimizationService.CreateRunAsync(
                request.UserId!.Value,
                new RiftboundDeckOptimizationRunRequest(
                    request.PopulationSize,
                    request.Generations,
                    request.SeedsPerMatch,
                    request.MaxAutoplaySteps,
                    request.Seed
                ),
                cancellationToken
            );
        }
    }
}

public sealed class CreateRiftboundDeckOptimizationRunEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                CreateRiftboundDeckOptimizationRun.Endpoint,
                async (
                    CreateRiftboundDeckOptimizationRun.Command command,
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
            .Produces<RiftboundDeckOptimizationRunDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .WithName("CreateRiftboundDeckOptimizationRun")
            .WithTags("Riftbound AI");
    }
}
