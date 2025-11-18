using Microsoft.AspNetCore.Http;

namespace Application.Shared.Extensions;

public static class ResultExtensions
{
    public static IResult ToHttpResult<T>(this Result<T> result) =>
        result.Status switch
        {
            ResultStatus.Ok => Results.Ok(result.Data),
            ResultStatus.Conflict => Results.Conflict(result.ErrorMessage),
            ResultStatus.NotFound => Results.NotFound(result.ErrorMessage),
            _ => Results.BadRequest(result.ErrorMessage),
        };
}
