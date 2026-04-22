using System.Globalization;
using System.Text;
using System.Text.Json;
using Domain.Services.Trading;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services.Trading;

public sealed class AlpacaTradingDataProvider : ITradingDataProvider
{
    private static readonly TimeZoneInfo MarketTimeZone = ResolveMarketTimeZone();

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

    public async Task<IReadOnlyCollection<TradingBarSnapshot>> GetBarsAsync(
        string symbol,
        DateTimeOffset start,
        DateTimeOffset? end = null,
        int limit = 500,
        CancellationToken cancellationToken = default
    )
    {
        var normalizedLimit = Math.Clamp(limit, 1, 1000);
        var startIso = Uri.EscapeDataString(start.UtcDateTime.ToString("O"));
        var endpoint = $"/v2/stocks/{symbol}/bars?timeframe=1Min&start={startIso}&limit={normalizedLimit}";
        if (end is not null)
        {
            var endIso = Uri.EscapeDataString(end.Value.UtcDateTime.ToString("O"));
            endpoint += $"&end={endIso}";
        }

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

    public async Task<IReadOnlyCollection<string>> GetWatchlistSymbolsAsync(
        string watchlistId,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(watchlistId))
        {
            return [];
        }

        using var response = await SendApiRequestAsync($"/v2/watchlists/{watchlistId}", cancellationToken);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (!json.RootElement.TryGetProperty("assets", out var assets))
        {
            return [];
        }

        var symbols = new List<string>();
        foreach (var asset in assets.EnumerateArray())
        {
            var symbol = GetString(asset, "symbol");
            if (!string.IsNullOrWhiteSpace(symbol))
            {
                symbols.Add(symbol.Trim().ToUpperInvariant());
            }
        }

        return symbols
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<DateTimeOffset?> GetWatchlistMarketOpenUtcAsync(
        string watchlistId,
        DateOnly tradingDate,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(watchlistId))
        {
            return null;
        }

        var watchlistSymbols = await GetWatchlistSymbolsAsync(watchlistId, cancellationToken);
        if (watchlistSymbols.Count == 0)
        {
            return null;
        }

        var session = await GetTradingSessionAsync(tradingDate, cancellationToken);
        return session?.OpenTimeUtc;
    }

    public async Task<TradingSessionSnapshot?> GetTradingSessionAsync(
        DateOnly tradingDate,
        CancellationToken cancellationToken = default
    )
    {
        var tradingDateIso = tradingDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        using var response = await SendApiRequestAsync(
            $"/v2/calendar?start={tradingDateIso}&end={tradingDateIso}",
            cancellationToken
        );
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (json.RootElement.ValueKind != JsonValueKind.Array || json.RootElement.GetArrayLength() == 0)
        {
            return null;
        }

        var calendarDay = json.RootElement[0];
        var openText = GetString(calendarDay, "open");
        var closeText = GetString(calendarDay, "close");
        if (!TryParseCalendarTime(openText, out var marketOpenTime))
        {
            return null;
        }

        if (!TryParseCalendarTime(closeText, out var marketCloseTime))
        {
            return null;
        }

        var marketOpenUtc = ToMarketDateTimeUtc(tradingDate, marketOpenTime);
        var marketCloseUtc = ToMarketDateTimeUtc(tradingDate, marketCloseTime);
        if (marketCloseUtc <= marketOpenUtc)
        {
            return null;
        }

        return new TradingSessionSnapshot(tradingDate, marketOpenUtc, marketCloseUtc);
    }

    public async Task<TradingMarketClockSnapshot> GetMarketClockAsync(
        CancellationToken cancellationToken = default
    )
    {
        using var response = await SendApiRequestAsync("/v2/clock", cancellationToken);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = json.RootElement;

        return new TradingMarketClockSnapshot(
            GetBoolean(root, "is_open"),
            GetDateTimeOffset(root, "timestamp"),
            GetDateTimeOffset(root, "next_open"),
            GetDateTimeOffset(root, "next_close")
        );
    }

    public async Task<IReadOnlyCollection<TradingOrderSnapshot>> GetOpenOrdersAsync(
        CancellationToken cancellationToken = default
    )
    {
        using var response = await SendApiRequestAsync(
            "/v2/orders?status=open&nested=true&direction=desc&limit=500",
            cancellationToken
        );
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var orders = new List<TradingOrderSnapshot>();
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object)
            {
                orders.Add(ParseOrder(item));
            }
        }

        return orders;
    }

    public async Task<TradingOrderSubmissionResult> SubmitBracketOrderAsync(
        TradingBracketOrderRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var side = request.Direction == TradingDirection.Bullish ? "buy" : "sell";
        var payload = new
        {
            symbol = request.Symbol.ToUpperInvariant(),
            qty = request.Quantity,
            side,
            type = "market",
            time_in_force = "day",
            order_class = "bracket",
            stop_loss = new
            {
                stop_price = Math.Round(request.StopLossPrice, 2),
            },
            take_profit = new
            {
                limit_price = Math.Round(request.TakeProfitPrice, 2),
            },
        };

        var json = JsonSerializer.Serialize(payload);
        using var response = await SendApiRequestAsync(
            "/v2/orders",
            HttpMethod.Post,
            new StringContent(json, Encoding.UTF8, "application/json"),
            cancellationToken
        );
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = doc.RootElement;

        return new TradingOrderSubmissionResult(
            GetString(root, "id"),
            GetString(root, "symbol"),
            GetString(root, "status"),
            GetString(root, "side"),
            GetDecimal(root, "qty")
        );
    }

    public async Task<TradingOrderSnapshot?> GetOrderAsync(
        string orderId,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(orderId))
        {
            return null;
        }

        using var response = await SendApiRequestAsync(
            $"/v2/orders/{orderId.Trim()}?nested=true",
            cancellationToken
        );
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = doc.RootElement;

        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return ParseOrder(root);
    }

    private async Task<HttpResponseMessage> SendApiRequestAsync(
        string path,
        CancellationToken cancellationToken
    )
    {
        var url = BuildUrl(_options.Value.BaseUrl, path);
        return await SendRequestAsync(url, HttpMethod.Get, null, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendDataRequestAsync(
        string path,
        CancellationToken cancellationToken
    )
    {
        var resolvedPath = AppendFeed(path, _options.Value.MarketDataFeed);
        var url = BuildUrl(_options.Value.MarketDataUrl, resolvedPath);
        return await SendRequestAsync(url, HttpMethod.Get, null, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendApiRequestAsync(
        string path,
        HttpMethod method,
        HttpContent? content,
        CancellationToken cancellationToken
    )
    {
        var url = BuildUrl(_options.Value.BaseUrl, path);
        return await SendRequestAsync(url, method, content, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendRequestAsync(
        string url,
        HttpMethod method,
        HttpContent? content,
        CancellationToken cancellationToken
    )
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.TryAddWithoutValidation("APCA-API-KEY-ID", _options.Value.ApiKey);
        request.Headers.TryAddWithoutValidation("APCA-API-SECRET-KEY", _options.Value.ApiSecret);
        request.Content = content;

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return response;
    }

    private static string BuildUrl(string baseUrl, string path)
    {
        var normalizedBaseUrl = baseUrl.TrimEnd('/');
        var normalizedPath = path.TrimStart('/');

        if (
            normalizedBaseUrl.EndsWith("/v2", StringComparison.OrdinalIgnoreCase)
            && normalizedPath.StartsWith("v2/", StringComparison.OrdinalIgnoreCase)
        )
        {
            normalizedPath = normalizedPath["v2/".Length..];
        }

        return $"{normalizedBaseUrl}/{normalizedPath}";
    }

    private static string AppendFeed(string path, string? marketDataFeed)
    {
        var normalizedFeed = marketDataFeed?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedFeed))
        {
            return path;
        }

        if (path.Contains("feed=", StringComparison.OrdinalIgnoreCase))
        {
            return path;
        }

        var separator = path.Contains('?') ? '&' : '?';
        return $"{path}{separator}feed={Uri.EscapeDataString(normalizedFeed)}";
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

    private static bool GetBoolean(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return false;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => bool.TryParse(value.GetString(), out var parsed) && parsed,
            _ => false,
        };
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

    private static TradingOrderSnapshot ParseOrder(JsonElement element)
    {
        var legs = new List<TradingOrderSnapshot>();
        if (
            element.TryGetProperty("legs", out var legsElement)
            && legsElement.ValueKind == JsonValueKind.Array
        )
        {
            foreach (var leg in legsElement.EnumerateArray())
            {
                if (leg.ValueKind == JsonValueKind.Object)
                {
                    legs.Add(ParseOrder(leg));
                }
            }
        }

        return new TradingOrderSnapshot(
            GetString(element, "id"),
            GetString(element, "symbol"),
            GetString(element, "status"),
            GetString(element, "side"),
            GetString(element, "type"),
            GetDecimal(element, "qty"),
            GetDecimal(element, "filled_qty"),
            GetDecimal(element, "filled_avg_price"),
            GetNullableDateTimeOffset(element, "submitted_at"),
            GetNullableDateTimeOffset(element, "filled_at"),
            GetNullableDateTimeOffset(element, "canceled_at"),
            GetNullableDateTimeOffset(element, "updated_at"),
            legs
        );
    }

    private static DateTimeOffset? GetNullableDateTimeOffset(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            return DateTimeOffset.TryParse(value.GetString(), out var parsed) ? parsed : null;
        }

        return null;
    }

    private static bool TryParseCalendarTime(string value, out TimeSpan time)
    {
        return TimeSpan.TryParseExact(
                value,
                @"hh\:mm",
                CultureInfo.InvariantCulture,
                out time
            )
            || TimeSpan.TryParseExact(
                value,
                @"hh\:mm\:ss",
                CultureInfo.InvariantCulture,
                out time
            )
            || TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out time);
    }

    private static DateTimeOffset ToMarketDateTimeUtc(DateOnly date, TimeSpan localTime)
    {
        var local = new DateTime(
            date.Year,
            date.Month,
            date.Day,
            localTime.Hours,
            localTime.Minutes,
            0,
            DateTimeKind.Unspecified
        );
        var offset = MarketTimeZone.GetUtcOffset(local);
        return new DateTimeOffset(local, offset).ToUniversalTime();
    }

    private static TimeZoneInfo ResolveMarketTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        }
    }
}
