using Domain.Entities;
using Domain.Repositories;
using Domain.Services.Trading;
using Domain.Specifications.Trading;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Specifications.Trading;

public sealed class TradingTradesSpecification(IRepository<TradingTrade> repository)
    : BaseSpecification<TradingTrade>(repository),
        ITradingTradesSpecification
{
    public ITradingTradesSpecification ByFilters(
        string? symbol = null,
        TradingTradeStatus? status = null,
        TradingDirection? direction = null,
        string? exitReason = null,
        DateTimeOffset? submittedFromUtc = null,
        DateTimeOffset? submittedToUtc = null,
        decimal? minRealizedProfitLoss = null,
        decimal? maxRealizedProfitLoss = null
    )
    {
        var normalizedSymbol = symbol?.Trim().ToUpperInvariant();
        var normalizedExitReason = exitReason?.Trim();

        return (ITradingTradesSpecification)ApplyCriteria(x =>
            (string.IsNullOrWhiteSpace(normalizedSymbol) || x.Symbol == normalizedSymbol)
            && (!status.HasValue || x.Status == status.Value)
            && (!direction.HasValue || x.Direction == direction.Value)
            && (
                string.IsNullOrWhiteSpace(normalizedExitReason)
                || EF.Functions.ILike(x.ExitReason ?? string.Empty, $"%{normalizedExitReason}%")
            )
            && (!submittedFromUtc.HasValue || x.SubmittedAtUtc >= submittedFromUtc.Value)
            && (!submittedToUtc.HasValue || x.SubmittedAtUtc <= submittedToUtc.Value)
            && (
                !minRealizedProfitLoss.HasValue
                || (x.RealizedProfitLoss.HasValue && x.RealizedProfitLoss.Value >= minRealizedProfitLoss.Value)
            )
            && (
                !maxRealizedProfitLoss.HasValue
                || (x.RealizedProfitLoss.HasValue && x.RealizedProfitLoss.Value <= maxRealizedProfitLoss.Value)
            )
        );
    }

    public ITradingTradesSpecification OrderBySubmittedAt(bool ascending = false)
    {
        return (ITradingTradesSpecification)ApplyOrder(ascending, x => x.SubmittedAtUtc);
    }
}
