using Application.Common;
using Application.Common.Extensions;
using Application.Features.Trading.BackgroundServices;
using Application.Shared.Services.AI;
using Carter;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Application.Features.Trading.Commands;

public static class OpenTrade
{
    public const string Endpoint = "api/trading/open";

    public record OpenTradeCommand(

    ) : IRequest<Result<bool>>;

    public class Validator : AbstractValidator<OpenTradeCommand>
    {
        public Validator()
        {

        }
    }

    internal sealed class Handler(IAiService aiService)
        : IRequestHandler<OpenTradeCommand, Result<bool>>
    {
        public async Task<Result<bool>> Handle(OpenTradeCommand request, CancellationToken ct)
        {
            var result = await aiService.GetDailySignalAsync("USDCAD", 0);
            if(result == null)
                return Result<bool>.Failure("No Response received");
            ZmqTradeService.AddCommand(CommandType.Open, result.ToCommand());
            return Result<bool>.Success(true);
        }
    }
}

public class OpenTradeEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                OpenTrade.Endpoint,
                async (
                    OpenTrade.OpenTradeCommand cmd,
                    ISender sender
                ) =>
                {
                    var result = await sender.Send(cmd);
                    return result.ToHttpResult();
                }
            )
            .Produces<bool>()
            .Produces(StatusCodes.Status400BadRequest)
            .WithName("OpenTrade")
            .WithTags("Trading");
    }
}
