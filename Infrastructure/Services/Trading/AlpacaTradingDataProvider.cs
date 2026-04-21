using System.Globalization;
using System.Text.Json;
using Domain.Services.Trading;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services.Trading;

public sealed class AlpacaTradingDataProvider : ITradingDataProvider
{
    private readonly HttpClient _httpClient;
    private readonly IOptions<AlpacaTradingOptions> _options;

    public AlpacaTradingDataProvider(HttpClient httpClient, IOptions<AlpacaTradingOptions> options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    public async Task<TradingAccountSnapshot> GetAccountAsync(
        CancellationToken cancellationToken = default
    )
    {
        using var response = await SendApiRequestAsync("/v2/account", cancellationToken);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = json.RootElement;

        return new TradingAccountSnapshot(
            GetString(root, "id"),
            GetString(root, "status"),
            GetDecimal(root, "cash"),
            GetDecimal(root, "buying_power"),
            GetDecimal(root, "portfolio_value"),
            GetString(root, "currency")
        );
    }

    public async Task<IReadOnlyCollection<TradingPositionSnapshot>> GetPositionsAsync(
        CancellationToken cancellationToken = default
    )
    {
        using var response = await SendApiRequestAsync("/v2/positions", cancellationToken);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (json.RootElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var positions = new List<TradingPositionSnapshot>();
        foreach (var position in json.RootElement.EnumerateArray())
        {
            positions.Add(
                new TradingPositionSnapshot(
                    GetString(position, "symbol"),
                    GetDecimal(position, "qty"),
                    GetDecimal(position, "market_value"),
                    GetDecimal(position, "avg_entry_price"),
                    GetDecimal(position, "current_price"),
                    GetDecimal(position, "unrealized_pl")
                )
            );
        }

        return positions;
    }

    public async Task<TradingQuoteSnapshot> GetQuoteAsync(
        string symbol,
        CancellationToken cancellationToken = default
    )
    {
        var endpoint = $"/v2/stocks/{symbol}/quotes/latest";
        using var response = await SendDataRequestAsync(endpoint, cancellationToken);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var quote = json.RootElement.GetProperty("quote");

        var bidPrice = GetDecimal(quote, "bp");
        var askPrice = GetDecimal(quote, "ap");

        return new TradingQuoteSnapshot(
            symbol,
            bidPrice,
            askPrice,
            (bidPrice + askPrice) / 2m,
            GetDateTimeOffset(quote, "t")
        );
    }

    public async Task<IReadOnlyCollection<TradingBarSnapshot>> GetRecentBarsAsync(
        string symbol,
        int limit = 50,
        CancellationToken cancellationToken = default
    )
    {
        var endpoint = $"/v2/stocks/{symbol}/bars?timeframe=1Min&limit={Math.Clamp(limit, 1, 1000)}";
        using var response = await SendDataRequestAsync(endpoint, cancellationToken);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (!json.RootElement.TryGetProperty("bars", out var barsElement))
        {
            return [];
        }

        var bars = new List<TradingBarSnapshot>();
        foreach (var bar in barsElement.EnumerateArray())
        {
            bars.Add(
                new TradingBarSnapshot(
                    symbol,
                    GetDateTimeOffset(bar, "t"),
                    GetDecimal(bar, "o"),
                    GetDecimal(bar, "h"),
                    GetDecimal(bar, "l"),
                    GetDecimal(bar, "c"),
                    GetDecimal(bar, "v")
                )
            );
        }

        return bars;
    }

    private async Task<HttpResponseMessage> SendApiRequestAsync(
        string path,
        CancellationToken cancellationToken
    )
    {
        var url = BuildUrl(_options.Value.BaseUrl, path);
        return await SendRequestAsync(url, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendDataRequestAsync(
        string path,
        CancellationToken cancellationToken
    )
    {
        var url = BuildUrl(_options.Value.MarketDataUrl, path);
        return await SendRequestAsync(url, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendRequestAsync(
        string url,
        CancellationToken cancellationToken
    )
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("APCA-API-KEY-ID", _options.Value.ApiKey);
        request.Headers.TryAddWithoutValidation("APCA-API-SECRET-KEY", _options.Value.ApiSecret);

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return response;
    }

    private static string BuildUrl(string baseUrl, string path)
    {
        return $"{baseUrl.TrimEnd('/')}/{path.TrimStart('/')}";
    }

    private static string GetString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return string.Empty;
        }

        return value.GetString() ?? string.Empty;
    }

    private static decimal GetDecimal(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return 0m;
        }

        if (value.ValueKind == JsonValueKind.Number)
        {
            return value.GetDecimal();
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            return decimal.TryParse(value.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : 0m;
        }

        return 0m;
    }

    private static DateTimeOffset GetDateTimeOffset(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return DateTimeOffset.MinValue;
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            return DateTimeOffset.TryParse(value.GetString(), out var parsed)
                ? parsed
                : DateTimeOffset.MinValue;
        }

        return DateTimeOffset.MinValue;
    }
}
