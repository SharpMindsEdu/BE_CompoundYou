using Application.Shared;
using Application.Shared.Extensions;
using Carter;
using Domain.Services.Trading;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Application.Features.Trading.Queries;

public static class GetTradingTickerBars
{
    public const string Endpoint = "api/trading/ticker/bars";

    public sealed record Query(
        [FromQuery] string Symbol,
        [FromQuery] string Interval,
        [FromQuery] DateTimeOffset StartUtc,
        [FromQuery] DateTimeOffset EndUtc
    ) : IRequest<Result<Response>>;

    public sealed record Response(
        string Symbol,
        string Interval,
        DateTimeOffset StartUtc,
        DateTimeOffset EndUtc,
        IReadOnlyCollection<TradingBarSnapshot> Bars
    );

    public sealed class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(x => x.Symbol)
                .NotEmpty()
                .WithMessage("Symbol is required.");

            RuleFor(x => x.Interval)
                .NotEmpty()
                .WithMessage("Interval is required.");

            RuleFor(x => x.Interval)
                .Must(x => TradingBarIntervalParser.TryParse(x, out _))
                .WithMessage(
                    "Interval must be a positive value such as '1min', '5min', '15min', '1h', or '1d'."
                );

            RuleFor(x => x.EndUtc)
                .GreaterThanOrEqualTo(x => x.StartUtc)
                .WithMessage("EndUtc must be greater than or equal to StartUtc.");
        }
    }

    internal sealed class Handler(ITradingDataProvider tradingDataProvider)
        : IRequestHandler<Query, Result<Response>>
    {
        public async Task<Result<Response>> Handle(
            Query request,
            CancellationToken cancellationToken
        )
        {
            var normalizedSymbol = request.Symbol.Trim().ToUpperInvariant();
            var interval = TradingBarIntervalParser.Parse(request.Interval);
            var bars = await tradingDataProvider.GetBarsInRangeAsync(
                normalizedSymbol,
                interval,
                request.StartUtc,
                request.EndUtc,
                cancellationToken
            );

            return Result<Response>.Success(
                new Response(
                    normalizedSymbol,
                    interval.Canonical,
                    request.StartUtc,
                    request.EndUtc,
                    bars
                )
            );
        }
    }
}

public sealed class GetTradingTickerBarsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                GetTradingTickerBars.Endpoint,
                async ([AsParameters] GetTradingTickerBars.Query query, ISender sender) =>
                {
                    var result = await sender.Send(query);
                    return result.ToHttpResult();
                }
            )
            .RequireAuthorization()
            .Produces<GetTradingTickerBars.Response>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .WithName("GetTradingTickerBars")
            .WithTags("Trading");
    }
}
