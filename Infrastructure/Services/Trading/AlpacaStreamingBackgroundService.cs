using System.Buffers;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Application.Features.Trading.Live;
using Domain.Services.Trading;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services.Trading;

public interface IAlpacaStreamingCache
{
    bool IsEnabled { get; }

    void SetSymbols(IReadOnlyCollection<string> symbols);

    bool TryGetQuote(string symbol, out TradingQuoteSnapshot quote);

    bool TryGetOrder(string orderId, out TradingOrderSnapshot order);

    IReadOnlyCollection<TradingBarSnapshot> GetBars(
        string symbol,
        DateTimeOffset start,
        DateTimeOffset? end,
        int limit
    );
}

public sealed class AlpacaStreamingBackgroundService : BackgroundService, IAlpacaStreamingCache
{
    private readonly ConcurrentDictionary<string, TradingQuoteSnapshot> _quotes = new(
        StringComparer.OrdinalIgnoreCase
    );
    private readonly ConcurrentDictionary<string, TradingOrderSnapshot> _orders = new(
        StringComparer.OrdinalIgnoreCase
    );
    private readonly ConcurrentDictionary<string, SymbolBarBuffer> _bars = new(
        StringComparer.OrdinalIgnoreCase
    );
    private readonly ILogger<AlpacaStreamingBackgroundService> _logger;
    private readonly IOptions<AlpacaTradingOptions> _options;
    private readonly ITradingTickerUpdateChannel _tickerUpdateChannel;
    private readonly object _symbolsGate = new();
    private readonly TimeSpan _subscriptionSyncInterval = TimeSpan.FromSeconds(2);
    private HashSet<string> _desiredSymbols = new(StringComparer.OrdinalIgnoreCase);

    public AlpacaStreamingBackgroundService(
        IOptions<AlpacaTradingOptions> options,
        ITradingTickerUpdateChannel tickerUpdateChannel,
        ILogger<AlpacaStreamingBackgroundService> logger
    )
    {
        _options = options;
        _tickerUpdateChannel = tickerUpdateChannel;
        _logger = logger;
    }

    public bool IsEnabled => _options.Value.UseStreamingApi;

    public void SetSymbols(IReadOnlyCollection<string> symbols)
    {
        var normalized = symbols
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        lock (_symbolsGate)
        {
            _desiredSymbols = normalized;
        }
    }

    public bool TryGetQuote(string symbol, out TradingQuoteSnapshot quote)
    {
        return _quotes.TryGetValue(symbol, out quote!);
    }

    public bool TryGetOrder(string orderId, out TradingOrderSnapshot order)
    {
        return _orders.TryGetValue(orderId, out order!);
    }

    public IReadOnlyCollection<TradingBarSnapshot> GetBars(
        string symbol,
        DateTimeOffset start,
        DateTimeOffset? end,
        int limit
    )
    {
        if (!_bars.TryGetValue(symbol, out var buffer))
        {
            return [];
        }

        lock (buffer.Gate)
        {
            return buffer.Bars
                .Where(x => x.Timestamp >= start && (end is null || x.Timestamp <= end.Value))
                .OrderBy(x => x.Timestamp)
                .TakeLast(Math.Clamp(limit, 1, 1000))
                .ToArray();
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!IsEnabled)
        {
            _logger.LogInformation("Alpaca streaming cache is disabled.");
            return;
        }

        var tasks = new List<Task>(2);
        if (_options.Value.UseTradingStream)
        {
            tasks.Add(RunTradingStreamLoopAsync(stoppingToken));
        }

        if (_options.Value.UseMarketDataStream)
        {
            tasks.Add(RunMarketDataStreamLoopAsync(stoppingToken));
        }

        if (tasks.Count == 0)
        {
            _logger.LogInformation("Alpaca streaming cache started with all streams disabled.");
            return;
        }

        await Task.WhenAll(tasks);
    }

    private async Task RunTradingStreamLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await RunTradingStreamConnectionAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Trading stream connection failed; reconnecting.");
            }

            var delaySeconds = Math.Max(1, _options.Value.StreamingReconnectDelaySeconds);
            await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
        }
    }

    private async Task RunMarketDataStreamLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await RunMarketDataConnectionAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Market data stream connection failed; reconnecting.");
            }

            var delaySeconds = Math.Max(1, _options.Value.StreamingReconnectDelaySeconds);
            await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
        }
    }

    private async Task RunTradingStreamConnectionAsync(CancellationToken cancellationToken)
    {
        if (
            string.IsNullOrWhiteSpace(_options.Value.ApiKey)
            || string.IsNullOrWhiteSpace(_options.Value.ApiSecret)
        )
        {
            _logger.LogWarning("Trading stream skipped because Alpaca API credentials are missing.");
            return;
        }

        using var socket = new ClientWebSocket();
        await socket.ConnectAsync(new Uri(_options.Value.TradingStreamUrl.Trim()), cancellationToken);
        _logger.LogInformation("Connected to Alpaca trading stream.");

        await SendJsonAsync(
            socket,
            new
            {
                action = "auth",
                key = _options.Value.ApiKey,
                secret = _options.Value.ApiSecret,
            },
            cancellationToken
        );
        await SendJsonAsync(
            socket,
            new
            {
                action = "listen",
                data = new
                {
                    streams = new[] { "trade_updates" },
                },
            },
            cancellationToken
        );

        while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            var payload = await ReceiveMessageAsync(socket, cancellationToken);
            if (string.IsNullOrWhiteSpace(payload))
            {
                continue;
            }

            try
            {
                ProcessTradingStreamPayload(payload);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to parse trading stream payload.");
            }
        }
    }

    private async Task RunMarketDataConnectionAsync(CancellationToken cancellationToken)
    {
        if (
            string.IsNullOrWhiteSpace(_options.Value.ApiKey)
            || string.IsNullOrWhiteSpace(_options.Value.ApiSecret)
        )
        {
            _logger.LogWarning("Market data stream skipped because Alpaca API credentials are missing.");
            return;
        }

        using var socket = new ClientWebSocket();
        await socket.ConnectAsync(new Uri(_options.Value.MarketDataStreamUrl.Trim()), cancellationToken);
        _logger.LogInformation("Connected to Alpaca market data stream.");

        await SendJsonAsync(
            socket,
            new
            {
                action = "auth",
                key = _options.Value.ApiKey,
                secret = _options.Value.ApiSecret,
            },
            cancellationToken
        );

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var subscriptionTask = RunSubscriptionSyncLoopAsync(socket, linkedCts.Token);

        try
        {
            while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                var payload = await ReceiveMessageAsync(socket, cancellationToken);
                if (string.IsNullOrWhiteSpace(payload))
                {
                    continue;
                }

                try
                {
                    ProcessMarketDataPayload(payload);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to parse market data stream payload.");
                }
            }
        }
        finally
        {
            linkedCts.Cancel();
            try
            {
                await subscriptionTask;
            }
            catch (OperationCanceledException)
            {
                // expected on shutdown/reconnect
            }
        }
    }

    private async Task RunSubscriptionSyncLoopAsync(
        ClientWebSocket socket,
        CancellationToken cancellationToken
    )
    {
        var subscribedSymbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            HashSet<string> desiredSymbols;
            lock (_symbolsGate)
            {
                desiredSymbols = new HashSet<string>(_desiredSymbols, StringComparer.OrdinalIgnoreCase);
            }

            var symbolsToSubscribe = desiredSymbols.Except(subscribedSymbols).ToArray();
            var symbolsToUnsubscribe = subscribedSymbols.Except(desiredSymbols).ToArray();

            if (symbolsToSubscribe.Length > 0)
            {
                await SendJsonAsync(
                    socket,
                    new
                    {
                        action = "subscribe",
                        quotes = symbolsToSubscribe,
                        bars = symbolsToSubscribe,
                    },
                    cancellationToken
                );
                foreach (var symbol in symbolsToSubscribe)
                {
                    subscribedSymbols.Add(symbol);
                }
            }

            if (symbolsToUnsubscribe.Length > 0)
            {
                await SendJsonAsync(
                    socket,
                    new
                    {
                        action = "unsubscribe",
                        quotes = symbolsToUnsubscribe,
                        bars = symbolsToUnsubscribe,
                    },
                    cancellationToken
                );
                foreach (var symbol in symbolsToUnsubscribe)
                {
                    subscribedSymbols.Remove(symbol);
                }
            }

            await Task.Delay(_subscriptionSyncInterval, cancellationToken);
        }
    }

    private void ProcessTradingStreamPayload(string payload)
    {
        using var doc = JsonDocument.Parse(payload);
        if (doc.RootElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                ProcessTradingStreamItem(item);
            }

            return;
        }

        if (doc.RootElement.ValueKind == JsonValueKind.Object)
        {
            ProcessTradingStreamItem(doc.RootElement);
        }
    }

    private void ProcessTradingStreamItem(JsonElement item)
    {
        var streamName = item.TryGetProperty("stream", out var stream) ? stream.GetString() : null;
        if (
            !string.Equals(streamName, "trade_updates", StringComparison.OrdinalIgnoreCase)
            || !item.TryGetProperty("data", out var data)
            || data.ValueKind != JsonValueKind.Object
        )
        {
            return;
        }

        if (!data.TryGetProperty("order", out var orderElement) || orderElement.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var order = ParseOrder(orderElement);
        if (string.IsNullOrWhiteSpace(order.OrderId))
        {
            return;
        }

        _orders[order.OrderId] = order;
    }

    private void ProcessMarketDataPayload(string payload)
    {
        using var doc = JsonDocument.Parse(payload);
        if (doc.RootElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                ProcessMarketDataItem(item);
            }

            return;
        }

        if (doc.RootElement.ValueKind == JsonValueKind.Object)
        {
            ProcessMarketDataItem(doc.RootElement);
        }
    }

    private void ProcessMarketDataItem(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("T", out var typeElement))
        {
            return;
        }

        var type = typeElement.GetString() ?? string.Empty;
        if (type.Equals("q", StringComparison.OrdinalIgnoreCase))
        {
            var symbol = GetString(item, "S");
            if (string.IsNullOrWhiteSpace(symbol))
            {
                return;
            }

            var bidPrice = GetDecimal(item, "bp");
            var askPrice = GetDecimal(item, "ap");
            var timestamp = GetDateTimeOffset(item, "t");
            var lastPrice = bidPrice > 0m && askPrice > 0m ? (bidPrice + askPrice) / 2m : Math.Max(bidPrice, askPrice);
            _quotes[symbol] = new TradingQuoteSnapshot(symbol, bidPrice, askPrice, lastPrice, timestamp);
            return;
        }

        if (type.Equals("b", StringComparison.OrdinalIgnoreCase))
        {
            var symbol = GetString(item, "S");
            if (string.IsNullOrWhiteSpace(symbol))
            {
                return;
            }

            var bar = new TradingBarSnapshot(
                symbol,
                GetDateTimeOffset(item, "t"),
                GetDecimal(item, "o"),
                GetDecimal(item, "h"),
                GetDecimal(item, "l"),
                GetDecimal(item, "c"),
                GetDecimal(item, "v")
            );

            AddBar(symbol, bar);
        }
    }

    private void AddBar(string symbol, TradingBarSnapshot bar)
    {
        var maxBars = Math.Max(100, _options.Value.StreamingMaxBarsPerSymbol);
        var buffer = _bars.GetOrAdd(symbol, _ => new SymbolBarBuffer());
        lock (buffer.Gate)
        {
            if (buffer.Bars.Count > 0 && buffer.Bars[^1].Timestamp == bar.Timestamp)
            {
                buffer.Bars[^1] = bar;
            }
            else
            {
                buffer.Bars.Add(bar);
            }

            while (buffer.Bars.Count > maxBars)
            {
                buffer.Bars.RemoveAt(0);
            }
        }

        _tickerUpdateChannel.TryPublish(bar);
    }

    private static async Task SendJsonAsync(
        ClientWebSocket socket,
        object payload,
        CancellationToken cancellationToken
    )
    {
        var json = JsonSerializer.Serialize(payload);
        var bytes = Encoding.UTF8.GetBytes(json);
        await socket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken);
    }

    private static async Task<string?> ReceiveMessageAsync(
        ClientWebSocket socket,
        CancellationToken cancellationToken
    )
    {
        var pooled = ArrayPool<byte>.Shared.Rent(8192);
        try
        {
            using var stream = new MemoryStream();
            while (true)
            {
                var result = await socket.ReceiveAsync(
                    new ArraySegment<byte>(pooled),
                    cancellationToken
                );

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    return null;
                }

                stream.Write(pooled, 0, result.Count);
                if (result.EndOfMessage)
                {
                    break;
                }
            }

            return Encoding.UTF8.GetString(stream.ToArray());
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(pooled);
        }
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
            return decimal.TryParse(
                value.GetString(),
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out var parsed
            )
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

    private sealed class SymbolBarBuffer
    {
        public object Gate { get; } = new();

        public List<TradingBarSnapshot> Bars { get; } = [];
    }
}
