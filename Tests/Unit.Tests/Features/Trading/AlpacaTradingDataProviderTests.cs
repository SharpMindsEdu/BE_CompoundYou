using System.Net;
using System.Text;
using Domain.Services.Trading;
using Infrastructure.Services.Trading;
using Microsoft.Extensions.Options;

namespace Unit.Tests.Features.Trading;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.TradingTests)]
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
    public async Task GetPositionsAsync_ParsesPositions()
    {
        var handler = new RouteHttpMessageHandler(_ =>
            "[{\"symbol\":\"TSLA\",\"qty\":\"3\",\"market_value\":\"750\",\"avg_entry_price\":\"240\",\"current_price\":\"250\",\"unrealized_pl\":\"30\"}]"
        );
        var provider = BuildProvider(handler, marketDataFeed: "iex");

        var positions = await provider.GetPositionsAsync();

        var position = Assert.Single(positions);
        Assert.Equal("TSLA", position.Symbol);
        Assert.Equal(3m, position.Quantity);
        Assert.Equal(30m, position.UnrealizedProfitLoss);
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

    [Fact]
    public async Task SelectOptionContractAsync_PicksNearestExpiryThenStrike()
    {
        var handler = new RouteHttpMessageHandler(request =>
        {
            var url = request.RequestUri?.AbsoluteUri ?? string.Empty;
            if (url.Contains("/v2/options/contracts", StringComparison.OrdinalIgnoreCase))
            {
                return """
                    {
                      "option_contracts": [
                        {
                          "symbol": "TSLA260515C00100000",
                          "underlying_symbol": "TSLA",
                          "type": "call",
                          "expiration_date": "2026-05-15",
                          "strike_price": "100",
                          "tradable": true
                        },
                        {
                          "symbol": "TSLA260522C00095000",
                          "underlying_symbol": "TSLA",
                          "type": "call",
                          "expiration_date": "2026-05-22",
                          "strike_price": "95",
                          "tradable": true
                        }
                      ]
                    }
                    """;
            }

            return "{}";
        });
        var provider = BuildProvider(handler, marketDataFeed: "iex", optionDataFeed: "indicative");

        var contract = await provider.SelectOptionContractAsync(
            "TSLA",
            TradingDirection.Bullish,
            98m,
            new DateOnly(2026, 5, 8),
            7,
            30
        );

        Assert.NotNull(contract);
        Assert.Equal("TSLA260515C00100000", contract!.Symbol);
        Assert.Equal(TradingOptionType.Call, contract.ContractType);
    }

    [Fact]
    public async Task GetOptionQuoteAsync_AppendsConfiguredOptionFeed()
    {
        var handler = new RouteHttpMessageHandler(request =>
        {
            var url = request.RequestUri?.AbsoluteUri ?? string.Empty;
            if (url.Contains("/v1beta1/options/quotes/latest", StringComparison.OrdinalIgnoreCase))
            {
                return """
                    {
                      "quotes": {
                        "TSLA260515C00100000": {
                          "bp": 1.10,
                          "ap": 1.30,
                          "t": "2026-05-08T14:31:00Z"
                        }
                      }
                    }
                    """;
            }

            return "{}";
        });
        var provider = BuildProvider(handler, marketDataFeed: "iex", optionDataFeed: "indicative");

        var quote = await provider.GetOptionQuoteAsync("TSLA260515C00100000");

        Assert.NotNull(quote);
        Assert.NotNull(handler.LastRequest);
        Assert.Contains("feed=indicative", handler.LastRequest!.RequestUri!.Query, StringComparison.Ordinal);
        Assert.Equal(1.10m, quote!.BidPrice);
        Assert.Equal(1.30m, quote.AskPrice);
    }

    [Fact]
    public async Task SubmitOptionOrderAsync_SendsExpectedPayload()
    {
        var handler = new RouteHttpMessageHandler(request =>
        {
            var url = request.RequestUri?.AbsoluteUri ?? string.Empty;
            if (url.Contains("/v2/orders", StringComparison.OrdinalIgnoreCase))
            {
                var body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                Assert.Contains("\"symbol\":\"TSLA260515C00100000\"", body, StringComparison.Ordinal);
                Assert.Contains("\"side\":\"buy\"", body, StringComparison.Ordinal);
                Assert.Contains("\"type\":\"market\"", body, StringComparison.Ordinal);
                Assert.Contains("\"time_in_force\":\"day\"", body, StringComparison.Ordinal);
                return """
                    {
                      "id": "option-order-1",
                      "symbol": "TSLA260515C00100000",
                      "status": "new",
                      "side": "buy",
                      "qty": "2"
                    }
                    """;
            }

            return "{}";
        });
        var provider = BuildProvider(handler, marketDataFeed: "iex", optionDataFeed: "indicative");

        var result = await provider.SubmitOptionOrderAsync(
            new TradingOptionOrderRequest("TSLA260515C00100000", TradingOrderSide.Buy, 2)
        );

        Assert.Equal("option-order-1", result.OrderId);
        Assert.Equal(2m, result.Quantity);
    }

    private static AlpacaTradingDataProvider BuildProvider(
        HttpMessageHandler messageHandler,
        string marketDataFeed,
        string optionDataFeed = "indicative"
    )
    {
        var client = new HttpClient(messageHandler);
        var options = Options.Create(
            new AlpacaTradingOptions
            {
                ApiKey = "PKN4EJQNOIZ2PQBCCCPLCSVFKS",
                ApiSecret = "WPm2uz3mwvcF5YhQ24Levz8BorpQbyThRfVjhBhdYBf",
                BaseUrl = "https://paper-api.alpaca.markets",
                MarketDataUrl = "https://data.alpaca.markets",
                MarketDataFeed = marketDataFeed,
                OptionDataFeed = optionDataFeed,
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
