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
        bool? UseTrailingStopLoss = null,
        bool? UseAiSentiment = null,
        bool? UseAiRetestValidation = null,
        int? MinOpportunities = null,
        int? MaxOpportunities = null,
        int? MinimumSentimentScore = null,
        int? MinimumRetestScore = null,
        int? MinimumMinutesFromMarketOpenForEntry = null,
        decimal? MinimumEntryDistanceFromRangeFraction = null,
        bool? AllowOppositeDirectionFallback = null,
        decimal? StartingEquity = null,
        decimal? StopLossBufferFraction = null,
        decimal? RewardToRiskRatio = null,
        decimal? OrderQuantity = null,
        decimal? RiskPerTradeFraction = null,
        bool? UseWholeShareQuantity = null,
        decimal? EstimatedSpreadBps = null,
        decimal? EstimatedSlippageBps = null,
        decimal? MarketOrderSpreadFillRatio = null,
        decimal? CommissionPerUnit = null,
        bool? UseAlpacaStandardFees = null,
        decimal? PartialTakeProfitFraction = null,
        decimal? TrailingStopRiskMultiple = null,
        bool? TrailingStopBreakEvenProtection = null,
        bool? UseCandleCache = null
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

            RuleFor(x => x.MinOpportunities)
                .InclusiveBetween(1, 50)
                .When(x => x.MinOpportunities.HasValue);

            RuleFor(x => x.MaxOpportunities)
                .InclusiveBetween(1, 50)
                .When(x => x.MaxOpportunities.HasValue);

            RuleFor(x => x.MinimumSentimentScore)
                .InclusiveBetween(1, 100)
                .When(x => x.MinimumSentimentScore.HasValue);

            RuleFor(x => x.MinimumRetestScore)
                .InclusiveBetween(1, 100)
                .When(x => x.MinimumRetestScore.HasValue);

            RuleFor(x => x.RewardToRiskRatio)
                .GreaterThan(0m)
                .When(x => x.RewardToRiskRatio.HasValue);

            RuleFor(x => x.MarketOrderSpreadFillRatio)
                .InclusiveBetween(0m, 1m)
                .When(x => x.MarketOrderSpreadFillRatio.HasValue);

            RuleFor(x => x.OrderQuantity)
                .GreaterThan(0m)
                .When(x => x.OrderQuantity.HasValue);

            RuleFor(x => x.RiskPerTradeFraction)
                .InclusiveBetween(0m, 0.1m)
                .When(x => x.RiskPerTradeFraction.HasValue);

            RuleFor(x => x.StopLossBufferFraction)
                .InclusiveBetween(0m, 0.2m)
                .When(x => x.StopLossBufferFraction.HasValue);
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
                    request.UseTrailingStopLoss,
                    request.UseAiSentiment,
                    request.UseAiRetestValidation,
                    request.MinOpportunities,
                    request.MaxOpportunities,
                    request.MinimumSentimentScore,
                    request.MinimumRetestScore,
                    request.MinimumMinutesFromMarketOpenForEntry,
                    request.MinimumEntryDistanceFromRangeFraction,
                    request.AllowOppositeDirectionFallback,
                    request.StartingEquity,
                    request.StopLossBufferFraction,
                    request.RewardToRiskRatio,
                    request.OrderQuantity,
                    request.RiskPerTradeFraction,
                    request.UseWholeShareQuantity,
                    request.EstimatedSpreadBps,
                    request.EstimatedSlippageBps,
                    request.MarketOrderSpreadFillRatio,
                    request.CommissionPerUnit,
                    request.UseAlpacaStandardFees,
                    request.PartialTakeProfitFraction,
                    request.TrailingStopRiskMultiple,
                    request.TrailingStopBreakEvenProtection,
                    request.UseCandleCache
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
            .Produces<TradingBacktestResult>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .WithName("RunTradingBacktest")
            .WithTags("Trading")
            .WithOpenApi();
    }
}
