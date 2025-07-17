using Application.Common;
using Application.Common.Extensions;
using Application.Features.Trading.BackgroundServices;
using Carter;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Application.Features.Trading.Commands;

public static class CloseTrade
{
    public const string Endpoint = "api/trading/close";

    public record CloseTradeCommand(

    ) : IRequest<Result<bool>>;

    public class Validator : AbstractValidator<CloseTradeCommand>
    {
        public Validator()
        {

        }
    }

    internal sealed class Handler()
        : IRequestHandler<CloseTradeCommand, Result<bool>>
    {
        public async Task<Result<bool>> Handle(CloseTradeCommand request, CancellationToken ct)
        {
            ZmqTradeService.AddCommand(CommandType.Close, "71176146");
            return Result<bool>.Success(true);
        }
    }
}

public class CloseTradeEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                CloseTrade.Endpoint,
                async (
                    CloseTrade.CloseTradeCommand cmd,
                    ISender sender
                ) =>
                {
                    var result = await sender.Send(cmd);
                    return result.ToHttpResult();
                }
            )
            .Produces<bool>()
            .Produces(StatusCodes.Status400BadRequest)
            .WithName("CloseTrade")
            .WithTags("Trading");
    }
}
