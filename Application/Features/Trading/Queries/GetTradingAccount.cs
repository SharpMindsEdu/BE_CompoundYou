using Application.Shared;
using Application.Shared.Extensions;
using Carter;
using Domain.Services.Trading;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Application.Features.Trading.Queries;

public static class GetTradingAccount
{
    public const string Endpoint = "api/trading/account";

    public sealed record Query : IRequest<Result<TradingAccountSnapshot>>;

    internal sealed class Handler(ITradingDataProvider tradingDataProvider)
        : IRequestHandler<Query, Result<TradingAccountSnapshot>>
    {
        public async Task<Result<TradingAccountSnapshot>> Handle(
            Query request,
            CancellationToken cancellationToken
        )
        {
            var account = await tradingDataProvider.GetAccountAsync(cancellationToken);
            return Result<TradingAccountSnapshot>.Success(account);
        }
    }
}

public sealed class GetTradingAccountEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(GetTradingAccount.Endpoint, async (ISender sender) =>
            {
                var result = await sender.Send(new GetTradingAccount.Query());
                return result.ToHttpResult();
            })
            .RequireAuthorization()
            .Produces<TradingAccountSnapshot>()
            .Produces(StatusCodes.Status401Unauthorized)
            .WithName("GetTradingAccount")
            .WithTags("Trading");
    }
}
