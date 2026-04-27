using Domain.Services.Trading;
using Infrastructure.Services.Trading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Xunit.Sdk;

namespace Unit.Tests.Features.Trading;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.TradingTests)]
[Trait("category", ServiceTestCategories.TradingLiveTests)]
public sealed class AlpacaPaperEndpointIntegrationTests
{
    [Fact]
    public async Task GetMarketClockAsync_UsesLivePaperEndpoint()
    {
        var provider = CreateLiveProvider();

        var clock = await RunLiveAsync(() =>
            provider.GetMarketClockAsync(TestContext.Current.CancellationToken)
        );

        Assert.True(clock.Timestamp > DateTimeOffset.MinValue);
        Assert.True(clock.NextOpen > DateTimeOffset.MinValue);
        Assert.True(clock.NextClose > DateTimeOffset.MinValue);
    }

    [Fact]
    public async Task GetQuoteAndBarsAsync_UsesLivePaperEndpoints()
    {
        var provider = CreateLiveProvider();
        const string symbol = "SPY";

        var quote = await RunLiveAsync(() =>
            provider.GetQuoteAsync(symbol, TestContext.Current.CancellationToken)
        );
        var session = await FindRecentTradingSessionAsync(provider, TestContext.Current.CancellationToken);
        var bars = await RunLiveAsync(() =>
            provider.GetBarsAsync(
                symbol,
                session.OpenTimeUtc,
                session.CloseTimeUtc,
                limit: 20,
                cancellationToken: TestContext.Current.CancellationToken
            )
        );

        Assert.Equal(symbol, quote.Symbol);
        Assert.True(quote.LastPrice > 0m);
        if (bars.Count == 0)
        {
            throw SkipException.ForSkip(
                $"No bars returned by Alpaca for {symbol} in session {session.Date:yyyy-MM-dd}. Live market-data coverage can vary by account/feed."
            );
        }

        Assert.All(bars, bar => Assert.Equal(symbol, bar.Symbol));
    }

    [Fact]
    public async Task SelectOptionContractAndQuoteAsync_UsesLivePaperEndpoints()
    {
        var provider = CreateLiveProvider();
        const string symbol = "SPY";
        var quote = await RunLiveAsync(() =>
            provider.GetQuoteAsync(symbol, TestContext.Current.CancellationToken)
        );
        var tradingDate = DateOnly.FromDateTime(DateTime.UtcNow);

        var contract = await RunLiveAsync(() =>
            provider.SelectOptionContractAsync(
                symbol,
                TradingDirection.Bullish,
                quote.LastPrice,
                tradingDate,
                minDaysToExpiration: 7,
                maxDaysToExpiration: 45,
                cancellationToken: TestContext.Current.CancellationToken
            )
        );

        if (contract is null)
        {
            throw SkipException.ForSkip(
                "No option contract returned from Alpaca. Verify option market data access for this account."
            );
        }

        var optionQuote = await RunLiveAsync(() =>
            provider.GetOptionQuoteAsync(contract.Symbol, TestContext.Current.CancellationToken)
        );

        Assert.NotNull(optionQuote);
        Assert.Equal(contract.Symbol, optionQuote!.Symbol);
        Assert.True(
            optionQuote.LastPrice > 0m || optionQuote.BidPrice > 0m || optionQuote.AskPrice > 0m
        );
    }

    [Fact]
    public async Task WatchlistEndpoints_UseLivePaperEndpoints()
    {
        var settings = ResolveLiveSettings();
        if (string.IsNullOrWhiteSpace(settings.WatchlistId))
        {
            throw SkipException.ForSkip(
                "Watchlist id is missing. Set ALPACA_WATCHLIST_ID (or AlpacaTrading:WatchlistId in appsettings.Development.json)."
            );
        }

        var provider = CreateLiveProvider();
        var symbols = await RunLiveAsync(() =>
            provider.GetWatchlistSymbolsAsync(
                settings.WatchlistId,
                TestContext.Current.CancellationToken
            )
        );
        if (symbols.Count == 0)
        {
            throw SkipException.ForSkip(
                $"Watchlist '{settings.WatchlistId}' returned no symbols for this paper account."
            );
        }

        var session = await FindRecentTradingSessionAsync(provider, TestContext.Current.CancellationToken);
        var marketOpen = await RunLiveAsync(() =>
            provider.GetWatchlistMarketOpenUtcAsync(
                settings.WatchlistId,
                session.Date,
                TestContext.Current.CancellationToken
            )
        );

        Assert.NotEmpty(symbols);
        Assert.NotNull(marketOpen);
    }

    private static AlpacaTradingDataProvider CreateLiveProvider()
    {
        var settings = ResolveLiveSettings();
        var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        var options = Options.Create(
            new AlpacaTradingOptions
            {
                ApiKey = settings.ApiKey,
                ApiSecret = settings.ApiSecret,
                BaseUrl = settings.BaseUrl,
                MarketDataUrl = settings.MarketDataUrl,
                MarketDataFeed = settings.MarketDataFeed,
                OptionDataFeed = settings.OptionDataFeed,
            }
        );

        return new AlpacaTradingDataProvider(httpClient, options);
    }

    private static async Task<TradingSessionSnapshot> FindRecentTradingSessionAsync(
        AlpacaTradingDataProvider provider,
        CancellationToken cancellationToken
    )
    {
        for (var offset = 0; offset <= 10; offset++)
        {
            var date = DateOnly.FromDateTime(DateTime.UtcNow.Date).AddDays(-offset);
            var session = await RunLiveAsync(() => provider.GetTradingSessionAsync(date, cancellationToken));
            if (session is not null)
            {
                return session;
            }
        }

        throw SkipException.ForSkip("No recent trading session returned by Alpaca in the last 10 days.");
    }

    private static LiveAlpacaSettings ResolveLiveSettings()
    {
        var solutionRoot = FindSolutionRoot();
        var apiDirectory = Path.Combine(solutionRoot, "Api");
        var configuration = new ConfigurationBuilder()
            .SetBasePath(apiDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var apiKey = FirstNonEmpty(
            Environment.GetEnvironmentVariable("ALPACA_PAPER_API_KEY"),
            Environment.GetEnvironmentVariable("ALPACA_API_KEY"),
            configuration["AlpacaTrading:ApiKey"]
        );
        var apiSecret = FirstNonEmpty(
            Environment.GetEnvironmentVariable("ALPACA_PAPER_API_SECRET"),
            Environment.GetEnvironmentVariable("ALPACA_API_SECRET"),
            configuration["AlpacaTrading:ApiSecret"]
        );

        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(apiSecret))
        {
            throw SkipException.ForSkip(
                "Alpaca paper credentials are missing. Set ALPACA_PAPER_API_KEY and ALPACA_PAPER_API_SECRET (or AlpacaTrading credentials in appsettings.Development.json)."
            );
        }

        return new LiveAlpacaSettings(
            BaseUrl: FirstNonEmpty(
                Environment.GetEnvironmentVariable("ALPACA_BASE_URL"),
                configuration["AlpacaTrading:BaseUrl"],
                "https://paper-api.alpaca.markets"
            ),
            MarketDataUrl: FirstNonEmpty(
                Environment.GetEnvironmentVariable("ALPACA_MARKET_DATA_URL"),
                configuration["AlpacaTrading:MarketDataUrl"],
                "https://data.alpaca.markets"
            ),
            MarketDataFeed: FirstNonEmpty(
                Environment.GetEnvironmentVariable("ALPACA_MARKET_DATA_FEED"),
                configuration["AlpacaTrading:MarketDataFeed"],
                "iex"
            ),
            OptionDataFeed: FirstNonEmpty(
                Environment.GetEnvironmentVariable("ALPACA_OPTION_DATA_FEED"),
                configuration["AlpacaTrading:OptionDataFeed"],
                "indicative"
            ),
            ApiKey: apiKey.Trim(),
            ApiSecret: apiSecret.Trim(),
            WatchlistId: FirstNonEmpty(
                Environment.GetEnvironmentVariable("ALPACA_WATCHLIST_ID"),
                configuration["TradingAutomation:WatchlistId"]
            )
        );
    }

    private static string FindSolutionRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null && !dir.GetFiles("*.sln").Any())
        {
            dir = dir.Parent;
        }

        return dir?.FullName
            ?? throw new InvalidOperationException("Could not locate solution root for test configuration.");
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    }

    private static async Task<T> RunLiveAsync<T>(Func<Task<T>> operation)
    {
        try
        {
            return await operation();
        }
        catch (HttpRequestException ex) when (IsTlsCredentialIssue(ex))
        {
            throw SkipException.ForSkip(
                "Live Alpaca endpoint calls are unavailable in this environment due local TLS credential configuration. Run these tests on a machine with outbound TLS configured."
            );
        }
    }

    private static bool IsTlsCredentialIssue(HttpRequestException exception)
    {
        var error = exception.ToString();
        return error.Contains(
                "No credentials are available in the security package",
                StringComparison.OrdinalIgnoreCase
            )
            || error.Contains(
                "Im Sicherheitspaket sind keine Anmeldeinformationen verfügbar",
                StringComparison.OrdinalIgnoreCase
            );
    }

    private sealed record LiveAlpacaSettings(
        string BaseUrl,
        string MarketDataUrl,
        string MarketDataFeed,
        string OptionDataFeed,
        string ApiKey,
        string ApiSecret,
        string WatchlistId
    );
}
