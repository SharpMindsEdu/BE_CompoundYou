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

public static class GetRiftboundSimulation
{
    public const string Endpoint = "api/riftbound/simulations/{simulationId:long}";

    public record Query(long SimulationId, long? UserId) : IRequest<Result<RiftboundSimulationDto>>;

    public class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(x => x.SimulationId).GreaterThan(0);
            RuleFor(x => x.UserId).NotNull().GreaterThan(0);
        }
    }

    internal sealed class Handler(IRiftboundSimulationService simulationService)
        : IRequestHandler<Query, Result<RiftboundSimulationDto>>
    {
        public Task<Result<RiftboundSimulationDto>> Handle(
            Query request,
            CancellationToken cancellationToken
        )
        {
            return simulationService.GetSimulationAsync(
                request.UserId!.Value,
                request.SimulationId,
                cancellationToken
            );
        }
    }
}

public sealed class GetRiftboundSimulationEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                GetRiftboundSimulation.Endpoint,
                async (long simulationId, ISender sender, HttpContext httpContext) =>
                {
                    var query = new GetRiftboundSimulation.Query(
                        simulationId,
                        httpContext.GetUserId()
                    );
                    var result = await sender.Send(query);
                    return result.ToHttpResult();
                }
            )
            .RequireAuthorization()
            .Produces<RiftboundSimulationDto>()
            .Produces(StatusCodes.Status404NotFound)
            .WithName("GetRiftboundSimulation")
            .WithTags("Riftbound Simulation");
    }
}
