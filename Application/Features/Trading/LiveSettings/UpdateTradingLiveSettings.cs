using Application.Shared;
using Application.Shared.Extensions;
using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Application.Features.Trading.LiveSettings;

public static class UpdateTradingLiveSettings
{
    public const string Endpoint = "api/trading/live-settings";

    public sealed record Command(UpdateTradingLiveSettingsRequest Body)
        : ICommandRequest<Result<TradingLiveSettingsDto>>;

    internal sealed class Handler(ITradingLiveSettingsService settingsService)
        : IRequestHandler<Command, Result<TradingLiveSettingsDto>>
    {
        public async Task<Result<TradingLiveSettingsDto>> Handle(
            Command request,
            CancellationToken cancellationToken
        )
        {
            var dto = await settingsService.UpdateAsync(request.Body, cancellationToken);
            return Result<TradingLiveSettingsDto>.Success(dto);
        }
    }
}

public sealed class UpdateTradingLiveSettingsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(UpdateTradingLiveSettings.Endpoint, async (
                UpdateTradingLiveSettingsRequest body,
                ISender sender
            ) =>
            {
                var result = await sender.Send(new UpdateTradingLiveSettings.Command(body));
                return result.ToHttpResult();
            })
            .RequireAuthorization()
            .Produces<TradingLiveSettingsDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .WithName("UpdateTradingLiveSettings")
            .WithTags("Trading");
    }
}
