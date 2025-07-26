using Mapster;

namespace Application.Common;

public enum ResultStatus
{
    Ok,
    BadRequest,
    Conflict,
    Unauthorized,
    Forbidden,
    NotFound,
    // etc.
}

public class Result<T>
{
    public bool Succeeded { get; init; }
    public T? Data { get; init; }
    public string? ErrorMessage { get; init; }
    public ResultStatus Status { get; init; }

    private Result(bool succeeded, T? data, string? error, ResultStatus status)
    {
        Succeeded = succeeded;
        Data = data;
        ErrorMessage = error;
        Status = status;
    }

    public static Result<T> Success(object data) =>
        new(true, data.Adapt<T>(), null, ResultStatus.Ok);

    public static Result<T> Success(T data) => new(true, data, null, ResultStatus.Ok);

    public static Result<T> Failure(string error, ResultStatus status = ResultStatus.BadRequest) =>
        new(false, default, error, status);
}
