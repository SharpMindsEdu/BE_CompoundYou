using Application.Shared;
using Application.Shared.Extensions;
using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Application.Features.Trading.Live;

public static class GetTradingLiveSnapshot
{
    public const string Endpoint = "api/trading/live";

    public sealed record Query : IRequest<Result<TradingLiveSnapshot>>;

    internal sealed class Handler(ITradingLiveTelemetryChannel telemetryChannel)
        : IRequestHandler<Query, Result<TradingLiveSnapshot>>
    {
        public Task<Result<TradingLiveSnapshot>> Handle(
            Query request,
            CancellationToken cancellationToken
        )
        {
            return Task.FromResult(Result<TradingLiveSnapshot>.Success(telemetryChannel.GetLatest()));
        }
    }
}

public sealed class GetTradingLiveSnapshotEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(GetTradingLiveSnapshot.Endpoint, async (ISender sender) =>
            {
                var result = await sender.Send(new GetTradingLiveSnapshot.Query());
                return result.ToHttpResult();
            })
            .RequireAuthorization()
            .Produces<TradingLiveSnapshot>()
            .Produces(StatusCodes.Status401Unauthorized)
            .WithName("GetTradingLiveSnapshot")
            .WithTags("Trading");
    }
}
