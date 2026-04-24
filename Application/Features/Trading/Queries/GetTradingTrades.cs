using System.Text.Json;
using Application.Features.Trading.Automation;
using Application.Shared;
using Application.Shared.Extensions;
using Carter;
using Domain.Entities;
using Domain.Repositories;
using Domain.Services.Trading;
using Domain.Specifications.Trading;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Application.Features.Trading.Queries;

public static class GetTradingTrades
{
    public const string Endpoint = "api/trading/trades";

    public record Query(
        [FromQuery] string? Symbol = null,
        [FromQuery] TradingTradeStatus? Status = null,
        [FromQuery] TradingDirection? Direction = null,
        [FromQuery] string? ExitReason = null,
        [FromQuery] DateTimeOffset? SubmittedFromUtc = null,
        [FromQuery] DateTimeOffset? SubmittedToUtc = null,
        [FromQuery] decimal? MinRealizedProfitLoss = null,
        [FromQuery] decimal? MaxRealizedProfitLoss = null,
        [FromQuery] bool SortAscending = false,
        [FromQuery] int Page = 1,
        [FromQuery] int PageSize = 50
    ) : IRequest<Result<Page<TradingTradeListItemDto>>>;

    public sealed record TradingTradeListItemDto(
        long Id,
        string Symbol,
        TradingDirection Direction,
        TradingTradeStatus Status,
        decimal Quantity,
        decimal PlannedEntryPrice,
        decimal PlannedStopLossPrice,
        decimal PlannedTakeProfitPrice,
        decimal PlannedRiskPerUnit,
        decimal? ActualEntryPrice,
        decimal? ActualExitPrice,
        decimal? RealizedProfitLoss,
        decimal? RealizedGrossProfitLoss,
        decimal? RealizedTotalFees,
        decimal? RealizedAlpacaFees,
        decimal? RealizedSpreadCost,
        decimal? RealizedRMultiple,
        string? ExitReason,
        int? SentimentScore,
        int? RetestScore,
        TradingSignalInsights? SignalInsights,
        DateTimeOffset SubmittedAtUtc,
        DateTimeOffset? EntryFilledAtUtc,
        DateTimeOffset? ExitFilledAtUtc
    );

    public sealed class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(x => x.Page).GreaterThan(0);
            RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
            RuleFor(x => x.SubmittedToUtc)
                .GreaterThanOrEqualTo(x => x.SubmittedFromUtc!.Value)
                .When(x => x.SubmittedFromUtc.HasValue && x.SubmittedToUtc.HasValue)
                .WithMessage("SubmittedToUtc must be greater than or equal to SubmittedFromUtc.");
            RuleFor(x => x.MaxRealizedProfitLoss)
                .GreaterThanOrEqualTo(x => x.MinRealizedProfitLoss!.Value)
                .When(x => x.MinRealizedProfitLoss.HasValue && x.MaxRealizedProfitLoss.HasValue)
                .WithMessage(
                    "MaxRealizedProfitLoss must be greater than or equal to MinRealizedProfitLoss."
                );
        }
    }

    internal sealed class Handler(ITradingTradesSpecification specification)
        : IRequestHandler<Query, Result<Page<TradingTradeListItemDto>>>
    {
        public async Task<Result<Page<TradingTradeListItemDto>>> Handle(
            Query request,
            CancellationToken cancellationToken
        )
        {
            var page = await specification
                .ByFilters(
                    request.Symbol,
                    request.Status,
                    request.Direction,
                    request.ExitReason,
                    request.SubmittedFromUtc,
                    request.SubmittedToUtc,
                    request.MinRealizedProfitLoss,
                    request.MaxRealizedProfitLoss
                )
                .OrderBySubmittedAt(request.SortAscending)
                .ToPage(request.Page, request.PageSize, cancellationToken);

            var dtoPage = new Page<TradingTradeListItemDto>(
                page.CurrentPage,
                page.NextPage,
                page.TotalPages,
                page.PageSize,
                page.TotalItems,
                page.Items.Select(Map).ToArray()
            );
            return Result<Page<TradingTradeListItemDto>>.Success(dtoPage);
        }

        private static TradingTradeListItemDto Map(TradingTrade trade)
        {
            return new TradingTradeListItemDto(
                trade.Id,
                trade.Symbol,
                trade.Direction,
                trade.Status,
                trade.Quantity,
                trade.PlannedEntryPrice,
                trade.PlannedStopLossPrice,
                trade.PlannedTakeProfitPrice,
                trade.PlannedRiskPerUnit,
                trade.ActualEntryPrice,
                trade.ActualExitPrice,
                trade.RealizedProfitLoss,
                trade.RealizedGrossProfitLoss,
                trade.RealizedTotalFees,
                trade.RealizedAlpacaFees,
                trade.RealizedSpreadCost,
                trade.RealizedRMultiple,
                trade.ExitReason,
                trade.SentimentScore,
                trade.RetestScore,
                ParseSignalInsights(trade.SignalInsightsJson),
                trade.SubmittedAtUtc,
                trade.EntryFilledAtUtc,
                trade.ExitFilledAtUtc
            );
        }

        private static TradingSignalInsights? ParseSignalInsights(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<TradingSignalInsights>(
                    json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                );
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}

public sealed class GetTradingTradesEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                GetTradingTrades.Endpoint,
                async ([AsParameters] GetTradingTrades.Query query, ISender sender) =>
                {
                    var result = await sender.Send(query);
                    return result.ToHttpResult();
                }
            )
            .RequireAuthorization()
            .Produces<Page<GetTradingTrades.TradingTradeListItemDto>>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .WithName("GetTradingTrades")
            .WithTags("Trading");
    }
}
