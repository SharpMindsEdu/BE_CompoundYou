using Application.Shared;
using Application.Shared.Extensions;
using Carter;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Application.Features.Trading.Backtesting;

public static class RunTradingBacktest
{
    public const string Endpoint = "api/trading/backtest";

    public record Command(
        DateOnly StartDate,
        DateOnly EndDate,
        string? WatchlistId = null,
        int? MaxOpportunities = null,
        int? MinimumSentimentScore = null,
        int? MinimumRetestScore = null,
        bool UseAiSentiment = true,
        bool UseAiRetestValidation = true
    ) : ICommandRequest<Result<TradingBacktestResult>>;

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.StartDate)
                .LessThanOrEqualTo(x => x.EndDate)
                .WithMessage("StartDate must be less than or equal to EndDate.");

            RuleFor(x => x.StartDate)
                .Must((command, _) => command.EndDate.DayNumber - command.StartDate.DayNumber <= 365)
                .WithMessage("Backtest range must not exceed 365 days.");
        }
    }

    internal sealed class Handler(ITradingBacktestService backtestService)
        : IRequestHandler<Command, Result<TradingBacktestResult>>
    {
        public async Task<Result<TradingBacktestResult>> Handle(
            Command request,
            CancellationToken cancellationToken
        )
        {
            var result = await backtestService.RunAsync(
                new TradingBacktestRequest(
                    request.StartDate,
                    request.EndDate,
                    request.WatchlistId,
                    request.MaxOpportunities,
                    request.MinimumSentimentScore,
                    request.MinimumRetestScore,
                    request.UseAiSentiment,
                    request.UseAiRetestValidation
                ),
                cancellationToken
            );
            return Result<TradingBacktestResult>.Success(result);
        }
    }
}

public class RunTradingBacktestEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                RunTradingBacktest.Endpoint,
                async (RunTradingBacktest.Command command, ISender sender) =>
                {
                    var result = await sender.Send(command);
                    return result.ToHttpResult();
                }
            )
            .RequireAuthorization()
            .Produces<TradingBacktestResult>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .WithName("RunTradingBacktest")
            .WithTags("Trading")
            .WithOpenApi();
    }
}
