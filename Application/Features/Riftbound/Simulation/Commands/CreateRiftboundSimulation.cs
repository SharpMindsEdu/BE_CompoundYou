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

namespace Application.Features.Riftbound.Simulation.Commands;

public static class CreateRiftboundSimulation
{
    public const string Endpoint = "api/riftbound/simulations";

    public record Command(
        long? UserId,
        long ChallengerDeckId,
        long OpponentDeckId,
        long? Seed,
        string? ChallengerPolicy,
        string? OpponentPolicy
    ) : ICommandRequest<Result<RiftboundSimulationDto>>;

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.UserId).NotNull().GreaterThan(0);
            RuleFor(x => x.ChallengerDeckId).GreaterThan(0);
            RuleFor(x => x.OpponentDeckId).GreaterThan(0);
            RuleFor(x => x.Seed).GreaterThan(0).When(x => x.Seed.HasValue);
        }
    }

    internal sealed class Handler(IRiftboundSimulationService simulationService)
        : IRequestHandler<Command, Result<RiftboundSimulationDto>>
    {
        public Task<Result<RiftboundSimulationDto>> Handle(
            Command request,
            CancellationToken cancellationToken
        )
        {
            return simulationService.CreateSimulationAsync(
                request.UserId!.Value,
                new RiftboundSimulationCreateRequest(
                    request.ChallengerDeckId,
                    request.OpponentDeckId,
                    request.Seed,
                    request.ChallengerPolicy,
                    request.OpponentPolicy
                ),
                cancellationToken
            );
        }
    }
}

public sealed class CreateRiftboundSimulationEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                CreateRiftboundSimulation.Endpoint,
                async (
                    CreateRiftboundSimulation.Command command,
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
            .Produces<RiftboundSimulationDto>()
            .Produces(StatusCodes.Status404NotFound)
            .WithName("CreateRiftboundSimulation")
            .WithTags("Riftbound Simulation");
    }
}
