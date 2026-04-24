using Application.Features.Trading.Live;
using Infrastructure.Services.Trading;

namespace Unit.Tests.Features.Trading;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.TradingLiveTests)]
public sealed class TradingLiveTelemetryChannelTests
{
    [Fact]
    public async Task TryPublish_UpdatesLatestAndExposesSnapshotToReaders()
    {
        var channel = new TradingLiveTelemetryChannel();
        var expected = new TradingLiveSnapshot(
            DateTimeOffset.UtcNow,
            new DateOnly(2026, 4, 23),
            true,
            DateTimeOffset.UtcNow,
            true,
            []
        );

        var published = channel.TryPublish(expected);
        Assert.True(published);

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        TradingLiveSnapshot? received = null;
        await foreach (var snapshot in channel.ReadAllAsync(cts.Token))
        {
            received = snapshot;
            break;
        }

        Assert.NotNull(received);
        Assert.Equal(expected, received);
        Assert.Equal(expected, channel.GetLatest());
    }

    [Fact]
    public void GetLatest_BeforeAnyPublish_ReturnsEmptySnapshot()
    {
        var channel = new TradingLiveTelemetryChannel();

        var snapshot = channel.GetLatest();

        Assert.Equal(DateTimeOffset.MinValue, snapshot.GeneratedAtUtc);
        Assert.Null(snapshot.TradingDate);
        Assert.False(snapshot.WorkerEnabled);
        Assert.Null(snapshot.MarketOpenUtc);
        Assert.False(snapshot.MarketIsOpen);
        Assert.Empty(snapshot.Symbols);
    }
}
