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

public static class AutoplayRiftboundSimulation
{
    public const string Endpoint = "api/riftbound/simulations/{simulationId:long}/autoplay";

    public record Command(long SimulationId, long? UserId, int MaxSteps = 400)
        : ICommandRequest<Result<RiftboundSimulationDto>>;

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.SimulationId).GreaterThan(0);
            RuleFor(x => x.UserId).NotNull().GreaterThan(0);
            RuleFor(x => x.MaxSteps).InclusiveBetween(1, 2000);
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
            return simulationService.AutoPlayAsync(
                request.UserId!.Value,
                request.SimulationId,
                new RiftboundSimulationAutoplayRequest(request.MaxSteps),
                cancellationToken
            );
        }
    }
}

public sealed class AutoplayRiftboundSimulationEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                AutoplayRiftboundSimulation.Endpoint,
                async (
                    long simulationId,
                    AutoplayRiftboundSimulation.Command command,
                    ISender sender,
                    HttpContext httpContext
                ) =>
                {
                    var enriched = command with
                    {
                        SimulationId = simulationId,
                        UserId = httpContext.GetUserId(),
                    };
                    var result = await sender.Send(enriched);
                    return result.ToHttpResult();
                }
            )
            .RequireAuthorization()
            .Produces<RiftboundSimulationDto>()
            .Produces(StatusCodes.Status404NotFound)
            .WithName("AutoplayRiftboundSimulation")
            .WithTags("Riftbound Simulation");
    }
}
