using Application.Shared;
using Application.Shared.Extensions;
using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Application.Features.Trading.LiveSettings;

public static class GetTradingLiveSettings
{
    public const string Endpoint = "api/trading/live-settings";

    public sealed record Query : IRequest<Result<TradingLiveSettingsDto>>;

    internal sealed class Handler(ITradingLiveSettingsService settingsService)
        : IRequestHandler<Query, Result<TradingLiveSettingsDto>>
    {
        public async Task<Result<TradingLiveSettingsDto>> Handle(
            Query request,
            CancellationToken cancellationToken
        )
        {
            var dto = await settingsService.GetAsync(cancellationToken);
            return Result<TradingLiveSettingsDto>.Success(dto);
        }
    }
}

public sealed class GetTradingLiveSettingsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(GetTradingLiveSettings.Endpoint, async (ISender sender) =>
            {
                var result = await sender.Send(new GetTradingLiveSettings.Query());
                return result.ToHttpResult();
            })
            .RequireAuthorization()
            .Produces<TradingLiveSettingsDto>()
            .Produces(StatusCodes.Status401Unauthorized)
            .WithName("GetTradingLiveSettings")
            .WithTags("Trading");
    }
}
