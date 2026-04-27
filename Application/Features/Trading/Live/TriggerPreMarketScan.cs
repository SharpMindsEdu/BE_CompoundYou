using Application.Shared;
using Application.Shared.Extensions;
using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Application.Features.Trading.Live;

public static class TriggerPreMarketScan
{
    public const string Endpoint = "api/trading/live/premarket-scan";

    public sealed record Command : IRequest<Result<bool>>;

    internal sealed class Handler(IPreMarketScanTrigger trigger)
        : IRequestHandler<Command, Result<bool>>
    {
        public Task<Result<bool>> Handle(Command request, CancellationToken cancellationToken)
        {
            trigger.RequestScan();
            return Task.FromResult(Result<bool>.Success(true));
        }
    }
}

public sealed class TriggerPreMarketScanEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(TriggerPreMarketScan.Endpoint, async (ISender sender) =>
            {
                var result = await sender.Send(new TriggerPreMarketScan.Command());
                return result.ToHttpResult();
            })
            .RequireAuthorization()
            .Produces<bool>()
            .Produces(StatusCodes.Status401Unauthorized)
            .WithName("TriggerPreMarketScan")
            .WithTags("Trading");
    }
}
