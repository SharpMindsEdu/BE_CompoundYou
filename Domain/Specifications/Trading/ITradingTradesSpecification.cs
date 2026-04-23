using Domain.Entities;
using Domain.Repositories;
using Domain.Services.Trading;

namespace Domain.Specifications.Trading;

public interface ITradingTradesSpecification : ISpecification<TradingTrade>
{
    ITradingTradesSpecification ByFilters(
        string? symbol = null,
        TradingTradeStatus? status = null,
        TradingDirection? direction = null,
        string? exitReason = null,
        DateTimeOffset? submittedFromUtc = null,
        DateTimeOffset? submittedToUtc = null,
        decimal? minRealizedProfitLoss = null,
        decimal? maxRealizedProfitLoss = null
    );

    ITradingTradesSpecification OrderBySubmittedAt(bool ascending = false);
}
