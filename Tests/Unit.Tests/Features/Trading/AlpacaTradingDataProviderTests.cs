using System.Net;
using System.Text;
using Domain.Services.Trading;
using Infrastructure.Services.Trading;
using Microsoft.Extensions.Options;

namespace Unit.Tests.Features.Trading;

[Trait("category", ServiceTestCategories.UnitTests)]
public sealed class AlpacaTradingDataProviderTests
{
    [Fact]
    public async Task GetBarsAsync_AppendsConfiguredFeed_WhenFeedIsMissing()
    {
        var handler = new RouteHttpMessageHandler(_ =>
            "{\"bars\":[{\"t\":\"2026-04-21T13:30:00Z\",\"o\":1,\"h\":1,\"l\":1,\"c\":1,\"v\":1}]}"
        );
        var provider = BuildProvider(handler, marketDataFeed: "iex");

        await provider.GetBarsAsync("TSLA", DateTimeOffset.UtcNow, limit: 1);

        Assert.NotNull(handler.LastRequest);
        Assert.Contains("feed=iex", handler.LastRequest!.RequestUri!.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetBarsAsync_DoesNotAppendFeed_WhenFeedIsEmpty()
    {
        var handler = new RouteHttpMessageHandler(_ =>
            "{\"bars\":[{\"t\":\"2026-04-21T13:30:00Z\",\"o\":1,\"h\":1,\"l\":1,\"c\":1,\"v\":1}]}"
        );
        var provider = BuildProvider(handler, marketDataFeed: " ");

        await provider.GetBarsAsync("TSLA", DateTimeOffset.UtcNow, limit: 1);

        Assert.NotNull(handler.LastRequest);
        Assert.DoesNotContain("feed=", handler.LastRequest!.RequestUri!.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetOpenOrdersAsync_ParsesOrders()
    {
        var handler = new RouteHttpMessageHandler(_ =>
            "[{\"id\":\"order-1\",\"symbol\":\"TSLA\",\"status\":\"new\",\"side\":\"buy\",\"type\":\"market\",\"qty\":\"2\",\"filled_qty\":\"0\",\"filled_avg_price\":\"0\"}]"
        );
        var provider = BuildProvider(handler, marketDataFeed: "iex");

        var orders = await provider.GetOpenOrdersAsync();

        var order = Assert.Single(orders);
        Assert.Equal("order-1", order.OrderId);
        Assert.Equal("TSLA", order.Symbol);
    }

    [Fact]
    public async Task GetTradingSessionAsync_ParsesMarketOpenAndClose()
    {
        var handler = new RouteHttpMessageHandler(request =>
        {
            var url = request.RequestUri?.AbsoluteUri ?? string.Empty;
            if (url.Contains("/v2/calendar", StringComparison.OrdinalIgnoreCase))
            {
                return "[{\"open\":\"09:30\",\"close\":\"13:00\"}]";
            }

            return "[]";
        });
        var provider = BuildProvider(handler, marketDataFeed: "iex");

        var session = await provider.GetTradingSessionAsync(new DateOnly(2026, 11, 27));

        Assert.NotNull(session);
        Assert.True(session!.CloseTimeUtc > session.OpenTimeUtc);
        Assert.Equal(TimeSpan.FromHours(3.5), session.CloseTimeUtc - session.OpenTimeUtc);
    }

    [Fact]
    public async Task SubmitBracketOrderAsync_ThrowsAlpacaApiException_WithParsedDetails()
    {
        var handler = new RouteHttpMessageHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.Forbidden)
            {
                Content = new StringContent(
                    "{\"code\":40310000,\"message\":\"account is not authorized to trade\"}",
                    Encoding.UTF8,
                    "application/json"
                ),
            };
            response.Headers.TryAddWithoutValidation("x-request-id", "req-123");
            return response;
        });
        var provider = BuildProvider(handler, marketDataFeed: "iex");

        var exception = await Assert.ThrowsAsync<AlpacaApiException>(() =>
            provider.SubmitBracketOrderAsync(
                new TradingBracketOrderRequest("TSLA", TradingDirection.Bullish, 1m, 100m, 110m)
            )
        );

        Assert.Equal(HttpStatusCode.Forbidden, exception.StatusCode);
        Assert.Equal("40310000", exception.AlpacaCode);
        Assert.Equal("account is not authorized to trade", exception.AlpacaMessage);
        Assert.Equal("req-123", exception.RequestId);
    }

    private static AlpacaTradingDataProvider BuildProvider(
        HttpMessageHandler messageHandler,
        string marketDataFeed
    )
    {
        var client = new HttpClient(messageHandler);
        var options = Options.Create(
            new AlpacaTradingOptions
            {
                ApiKey = "key",
                ApiSecret = "secret",
                BaseUrl = "https://paper-api.alpaca.markets",
                MarketDataUrl = "https://data.alpaca.markets",
                MarketDataFeed = marketDataFeed,
            }
        );

        return new AlpacaTradingDataProvider(client, options);
    }

    private sealed class RouteHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;

        public RouteHttpMessageHandler(Func<HttpRequestMessage, string> responseFactory)
            : this(request =>
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        responseFactory(request),
                        Encoding.UTF8,
                        "application/json"
                    ),
                }
            ) { }

        public RouteHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            LastRequest = request;
            return Task.FromResult(_responseFactory(request));
        }
    }
}
