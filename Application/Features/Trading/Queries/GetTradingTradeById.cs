using Application.Shared;
using Application.Shared.Extensions;
using Carter;
using Domain.Entities;
using Domain.Repositories;
using Domain.Services.Trading;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Application.Features.Trading.Queries;

public static class GetTradingTradeById
{
    public const string Endpoint = "api/trading/trades/{id:long}";

    public record Query([FromRoute] long Id) : IRequest<Result<TradingTradeDetailsDto>>;

    public sealed record TradingTradeDetailsDto(
        long Id,
        string Symbol,
        TradingDirection Direction,
        TradingTradeStatus Status,
        string AlpacaOrderId,
        string? AlpacaTakeProfitOrderId,
        string? AlpacaStopLossOrderId,
        string? AlpacaExitOrderId,
        decimal Quantity,
        decimal PlannedEntryPrice,
        decimal PlannedStopLossPrice,
        decimal PlannedTakeProfitPrice,
        decimal PlannedRiskPerUnit,
        decimal? ActualEntryPrice,
        decimal? ActualExitPrice,
        decimal? RealizedProfitLoss,
        decimal? RealizedRMultiple,
        string? ExitReason,
        string? AlpacaOrderStatus,
        string? AlpacaExitOrderStatus,
        int? SentimentScore,
        int? RetestScore,
        DateTimeOffset? SignalRetestBarTimestampUtc,
        DateTimeOffset SubmittedAtUtc,
        DateTimeOffset? EntryFilledAtUtc,
        DateTimeOffset? ExitFilledAtUtc,
        string? AlpacaOrderPayloadJson,
        string? AlpacaExitOrderPayloadJson,
        DateTimeOffset CreatedOn,
        DateTimeOffset? UpdatedOn
    );

    public sealed class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(x => x.Id).GreaterThan(0);
        }
    }

    internal sealed class Handler(IRepository<TradingTrade> repository)
        : IRequestHandler<Query, Result<TradingTradeDetailsDto>>
    {
        public async Task<Result<TradingTradeDetailsDto>> Handle(
            Query request,
            CancellationToken cancellationToken
        )
        {
            var trade = await repository.GetById(request.Id);
            if (trade is null)
            {
                return Result<TradingTradeDetailsDto>.Failure(
                    $"Trading trade with id '{request.Id}' was not found.",
                    ResultStatus.NotFound
                );
            }

            return Result<TradingTradeDetailsDto>.Success(Map(trade));
        }

        private static TradingTradeDetailsDto Map(TradingTrade trade)
        {
            return new TradingTradeDetailsDto(
                trade.Id,
                trade.Symbol,
                trade.Direction,
                trade.Status,
                trade.AlpacaOrderId,
                trade.AlpacaTakeProfitOrderId,
                trade.AlpacaStopLossOrderId,
                trade.AlpacaExitOrderId,
                trade.Quantity,
                trade.PlannedEntryPrice,
                trade.PlannedStopLossPrice,
                trade.PlannedTakeProfitPrice,
                trade.PlannedRiskPerUnit,
                trade.ActualEntryPrice,
                trade.ActualExitPrice,
                trade.RealizedProfitLoss,
                trade.RealizedRMultiple,
                trade.ExitReason,
                trade.AlpacaOrderStatus,
                trade.AlpacaExitOrderStatus,
                trade.SentimentScore,
                trade.RetestScore,
                trade.SignalRetestBarTimestampUtc,
                trade.SubmittedAtUtc,
                trade.EntryFilledAtUtc,
                trade.ExitFilledAtUtc,
                trade.AlpacaOrderPayloadJson,
                trade.AlpacaExitOrderPayloadJson,
                trade.CreatedOn,
                trade.UpdatedOn
            );
        }
    }
}

public sealed class GetTradingTradeByIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                GetTradingTradeById.Endpoint,
                async ([AsParameters] GetTradingTradeById.Query query, ISender sender) =>
                {
                    var result = await sender.Send(query);
                    return result.ToHttpResult();
                }
            )
            .RequireAuthorization()
            .Produces<GetTradingTradeById.TradingTradeDetailsDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("GetTradingTradeById")
            .WithTags("Trading");
    }
}
