using Application.Features.Trading.Automation;
using Application.Shared;
using Application.Shared.Extensions;
using Carter;
using Domain.Services.Trading;
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
        TradingDirection? Direction = null,
        bool? UseAiRetestValidation = null,
        int? MinOpportunities = null,
        int? MaxOpportunities = null,
        int? MinimumSentimentScore = null,
        int? MinimumRetestScore = null,
        int? MinimumMinutesFromMarketOpenForEntry = null,
        int? MaximumMinutesFromMarketOpenForEntry = null,
        int? MaxMinutesBreakoutToRetest = null,
        int? MinCandlesBetweenBreakoutAndRetest = null,
        decimal? MinimumEntryDistanceFromRangeFraction = null,
        bool? AllowOppositeDirectionFallback = null,
        decimal? StartingEquity = null,
        decimal? StopLossBufferFraction = null,
        decimal? StopLossBufferAsRetestRangeFraction = null,
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
        bool? UseCandleCache = null,
        decimal? BreakEvenAtRMultiple = null,
        int? MaxBarsInTradeBeforeFlatExit = null,
        int? MaxTradesPerDay = null,
        decimal? MaxDailyLossFraction = null,
        decimal? MaxOpeningRangeFractionOfPrice = null,
        bool? StopSlippageOnGap = null,
        bool? SpreadBpsScaleByPrice = null,
        bool? UseDirectionalIndicatorFilter = null,
        bool? DirectionalIndicatorRequireAll = null,
        IReadOnlyList<DirectionalIndicatorMode>? DirectionalIndicatorModes = null
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
                    StartDate: request.StartDate,
                    EndDate: request.EndDate,
                    WatchlistId: request.WatchlistId,
                    UseTrailingStopLoss: request.UseTrailingStopLoss,
                    UseAiSentiment: request.UseAiSentiment,
                    Direction: request.Direction,
                    UseAiRetestValidation: request.UseAiRetestValidation,
                    MinOpportunities: request.MinOpportunities,
                    MaxOpportunities: request.MaxOpportunities,
                    MinimumSentimentScore: request.MinimumSentimentScore,
                    MinimumRetestScore: request.MinimumRetestScore,
                    MinimumMinutesFromMarketOpenForEntry: request.MinimumMinutesFromMarketOpenForEntry,
                    MaximumMinutesFromMarketOpenForEntry: request.MaximumMinutesFromMarketOpenForEntry,
                    MaxMinutesBreakoutToRetest: request.MaxMinutesBreakoutToRetest,
                    MinCandlesBetweenBreakoutAndRetest: request.MinCandlesBetweenBreakoutAndRetest,
                    MinimumEntryDistanceFromRangeFraction: request.MinimumEntryDistanceFromRangeFraction,
                    AllowOppositeDirectionFallback: request.AllowOppositeDirectionFallback,
                    StartingEquity: request.StartingEquity,
                    StopLossBufferFraction: request.StopLossBufferFraction,
                    StopLossBufferAsRetestRangeFraction: request.StopLossBufferAsRetestRangeFraction,
                    RewardToRiskRatio: request.RewardToRiskRatio,
                    OrderQuantity: request.OrderQuantity,
                    RiskPerTradeFraction: request.RiskPerTradeFraction,
                    UseWholeShareQuantity: request.UseWholeShareQuantity,
                    EstimatedSpreadBps: request.EstimatedSpreadBps,
                    EstimatedSlippageBps: request.EstimatedSlippageBps,
                    MarketOrderSpreadFillRatio: request.MarketOrderSpreadFillRatio,
                    CommissionPerUnit: request.CommissionPerUnit,
                    UseAlpacaStandardFees: request.UseAlpacaStandardFees,
                    PartialTakeProfitFraction: request.PartialTakeProfitFraction,
                    TrailingStopRiskMultiple: request.TrailingStopRiskMultiple,
                    TrailingStopBreakEvenProtection: request.TrailingStopBreakEvenProtection,
                    UseCandleCache: request.UseCandleCache,
                    BreakEvenAtRMultiple: request.BreakEvenAtRMultiple,
                    MaxBarsInTradeBeforeFlatExit: request.MaxBarsInTradeBeforeFlatExit,
                    MaxTradesPerDay: request.MaxTradesPerDay,
                    MaxDailyLossFraction: request.MaxDailyLossFraction,
                    MaxOpeningRangeFractionOfPrice: request.MaxOpeningRangeFractionOfPrice,
                    StopSlippageOnGap: request.StopSlippageOnGap,
                    SpreadBpsScaleByPrice: request.SpreadBpsScaleByPrice,
                    UseDirectionalIndicatorFilter: request.UseDirectionalIndicatorFilter,
                    DirectionalIndicatorRequireAll: request.DirectionalIndicatorRequireAll,
                    DirectionalIndicatorModes: request.DirectionalIndicatorModes
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
