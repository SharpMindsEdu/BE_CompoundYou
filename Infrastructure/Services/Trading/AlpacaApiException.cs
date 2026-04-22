using System.Net;

namespace Infrastructure.Services.Trading;

public sealed class AlpacaApiException : HttpRequestException
{
    public AlpacaApiException(
        string message,
        HttpStatusCode statusCode,
        string method,
        string url,
        string? responseBody = null,
        string? alpacaCode = null,
        string? alpacaMessage = null,
        string? requestId = null
    )
        : base(message, inner: null, statusCode)
    {
        Method = method;
        Url = url;
        ResponseBody = responseBody;
        AlpacaCode = alpacaCode;
        AlpacaMessage = alpacaMessage;
        RequestId = requestId;
    }

    public string Method { get; }

    public string Url { get; }

    public string? ResponseBody { get; }

    public string? AlpacaCode { get; }

    public string? AlpacaMessage { get; }

    public string? RequestId { get; }
}
