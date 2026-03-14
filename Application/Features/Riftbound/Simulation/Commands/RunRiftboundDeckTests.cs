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

public static class RunRiftboundDeckTests
{
    public const string Endpoint = "api/riftbound/deck-tests";

    public record Command(
        long? UserId,
        long ChallengerDeckId,
        long OpponentDeckId,
        IReadOnlyCollection<long>? Seeds,
        int? RunCount,
        string? ChallengerPolicy,
        string? OpponentPolicy,
        int MaxAutoplaySteps = 500
    ) : ICommandRequest<Result<RiftboundDeckTestsResultDto>>;

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.UserId).NotNull().GreaterThan(0);
            RuleFor(x => x.ChallengerDeckId).GreaterThan(0);
            RuleFor(x => x.OpponentDeckId).GreaterThan(0);
            RuleFor(x => x.RunCount).InclusiveBetween(1, 100).When(x => x.RunCount.HasValue);
            RuleFor(x => x.MaxAutoplaySteps).InclusiveBetween(1, 2000);
            RuleForEach(x => x.Seeds).GreaterThan(0);
        }
    }

    internal sealed class Handler(IRiftboundSimulationService simulationService)
        : IRequestHandler<Command, Result<RiftboundDeckTestsResultDto>>
    {
        public Task<Result<RiftboundDeckTestsResultDto>> Handle(
            Command request,
            CancellationToken cancellationToken
        )
        {
            return simulationService.RunDeckTestsAsync(
                request.UserId!.Value,
                new RiftboundDeckTestsRequest(
                    request.ChallengerDeckId,
                    request.OpponentDeckId,
                    request.Seeds,
                    request.RunCount,
                    request.ChallengerPolicy,
                    request.OpponentPolicy,
                    request.MaxAutoplaySteps
                ),
                cancellationToken
            );
        }
    }
}

public sealed class RunRiftboundDeckTestsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                RunRiftboundDeckTests.Endpoint,
                async (
                    RunRiftboundDeckTests.Command command,
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
            .Produces<RiftboundDeckTestsResultDto>()
            .Produces(StatusCodes.Status404NotFound)
            .WithName("RunRiftboundDeckTests")
            .WithTags("Riftbound Simulation");
    }
}
