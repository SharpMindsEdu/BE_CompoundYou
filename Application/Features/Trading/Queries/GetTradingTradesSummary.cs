using Application.Shared;
using Application.Shared.Extensions;
using Carter;
using Domain.Entities;
using Domain.Services.Trading;
using Domain.Specifications.Trading;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Application.Features.Trading.Queries;

public static class GetTradingTradesSummary
{
    public const string Endpoint = "api/trading/trades/summary";

    public record Query(
        [FromQuery] string? Symbol = null,
        [FromQuery] TradingTradeStatus? Status = null,
        [FromQuery] TradingDirection? Direction = null,
        [FromQuery] string? ExitReason = null,
        [FromQuery] DateTimeOffset? SubmittedFromUtc = null,
        [FromQuery] DateTimeOffset? SubmittedToUtc = null,
        [FromQuery] decimal? MinRealizedProfitLoss = null,
        [FromQuery] decimal? MaxRealizedProfitLoss = null
    ) : IRequest<Result<TradingTradesSummaryDto>>;

    public sealed record TradingTradesSummaryDto(
        int TotalTrades,
        int SubmittedTrades,
        int EntryFilledTrades,
        int ClosedTrades,
        int WinningTrades,
        int LosingTrades,
        int BreakevenTrades,
        decimal TotalRealizedProfitLoss,
        decimal AverageRealizedProfitLoss,
        decimal AverageRealizedRMultiple,
        decimal WinRatePercent,
        DateTimeOffset? FirstSubmittedAtUtc,
        DateTimeOffset? LastSubmittedAtUtc
    );

    public sealed class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
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
        : IRequestHandler<Query, Result<TradingTradesSummaryDto>>
    {
        public async Task<Result<TradingTradesSummaryDto>> Handle(
            Query request,
            CancellationToken cancellationToken
        )
        {
            var trades = await specification
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
                .OrderBySubmittedAt()
                .ToList(cancellationToken);

            var total = trades.Count;
            var submitted = trades.Count(x => x.Status == TradingTradeStatus.Submitted);
            var entryFilled = trades.Count(x => x.Status == TradingTradeStatus.EntryFilled);
            var closed = trades.Where(x => x.Status == TradingTradeStatus.Closed).ToArray();

            var closedWithPnl = closed.Where(x => x.RealizedProfitLoss.HasValue).ToArray();
            var winning = closedWithPnl.Count(x => x.RealizedProfitLoss!.Value > 0m);
            var losing = closedWithPnl.Count(x => x.RealizedProfitLoss!.Value < 0m);
            var breakeven = closedWithPnl.Count(x => x.RealizedProfitLoss!.Value == 0m);

            var totalPnl = decimal.Round(closedWithPnl.Sum(x => x.RealizedProfitLoss!.Value), 6);
            var avgPnl = closedWithPnl.Length > 0
                ? decimal.Round(totalPnl / closedWithPnl.Length, 6)
                : 0m;

            var rMultiples = closed
                .Where(x => x.RealizedRMultiple.HasValue)
                .Select(x => x.RealizedRMultiple!.Value)
                .ToArray();
            var avgR = rMultiples.Length > 0 ? decimal.Round(rMultiples.Average(), 6) : 0m;

            var winRate = closedWithPnl.Length > 0
                ? decimal.Round((winning * 100m) / closedWithPnl.Length, 2)
                : 0m;

            var summary = new TradingTradesSummaryDto(
                total,
                submitted,
                entryFilled,
                closed.Length,
                winning,
                losing,
                breakeven,
                totalPnl,
                avgPnl,
                avgR,
                winRate,
                trades.MinBy(x => x.SubmittedAtUtc)?.SubmittedAtUtc,
                trades.MaxBy(x => x.SubmittedAtUtc)?.SubmittedAtUtc
            );

            return Result<TradingTradesSummaryDto>.Success(summary);
        }
    }
}

public sealed class GetTradingTradesSummaryEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                GetTradingTradesSummary.Endpoint,
                async ([AsParameters] GetTradingTradesSummary.Query query, ISender sender) =>
                {
                    var result = await sender.Send(query);
                    return result.ToHttpResult();
                }
            )
            .RequireAuthorization()
            .Produces<GetTradingTradesSummary.TradingTradesSummaryDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .WithName("GetTradingTradesSummary")
            .WithTags("Trading");
    }
}
