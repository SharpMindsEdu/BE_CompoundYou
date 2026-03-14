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

public static class ApplyRiftboundSimulationAction
{
    public const string Endpoint = "api/riftbound/simulations/{simulationId:long}/actions";

    public record Command(long SimulationId, long? UserId, string ActionId)
        : ICommandRequest<Result<RiftboundSimulationDto>>;

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.SimulationId).GreaterThan(0);
            RuleFor(x => x.UserId).NotNull().GreaterThan(0);
            RuleFor(x => x.ActionId).NotEmpty();
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
            return simulationService.ApplyActionAsync(
                request.UserId!.Value,
                request.SimulationId,
                request.ActionId,
                cancellationToken
            );
        }
    }
}

public sealed class ApplyRiftboundSimulationActionEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                ApplyRiftboundSimulationAction.Endpoint,
                async (
                    long simulationId,
                    ApplyRiftboundSimulationAction.Command command,
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
            .WithName("ApplyRiftboundSimulationAction")
            .WithTags("Riftbound Simulation");
    }
}
