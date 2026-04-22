using System.Net;
using System.Text;
using Infrastructure.Services.Trading;
using Microsoft.Extensions.Options;

namespace Unit.Tests.Features.Trading;

[Trait("category", ServiceTestCategories.UnitTests)]
public sealed class AlpacaTradingDataProviderTests
{
    [Fact]
    public async Task GetBarsAsync_AppendsConfiguredFeed_WhenFeedIsMissing()
    {
        var handler = new CaptureHttpMessageHandler();
        var provider = BuildProvider(handler, marketDataFeed: "iex");

        await provider.GetBarsAsync("TSLA", DateTimeOffset.UtcNow, limit: 1);

        Assert.NotNull(handler.LastRequest);
        Assert.Contains("feed=iex", handler.LastRequest!.RequestUri!.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetBarsAsync_DoesNotAppendFeed_WhenFeedIsEmpty()
    {
        var handler = new CaptureHttpMessageHandler();
        var provider = BuildProvider(handler, marketDataFeed: " ");

        await provider.GetBarsAsync("TSLA", DateTimeOffset.UtcNow, limit: 1);

        Assert.NotNull(handler.LastRequest);
        Assert.DoesNotContain("feed=", handler.LastRequest!.RequestUri!.Query, StringComparison.Ordinal);
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

    private sealed class CaptureHttpMessageHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            LastRequest = request;
            var payload = "{\"bars\":[{\"t\":\"2026-04-21T13:30:00Z\",\"o\":1,\"h\":1,\"l\":1,\"c\":1,\"v\":1}]}";
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json"),
            };

            return Task.FromResult(response);
        }
    }
}
