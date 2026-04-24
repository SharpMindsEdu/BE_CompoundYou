using Domain.Services.Trading;

namespace Unit.Tests.Features.Trading;

[Trait("category", ServiceTestCategories.UnitTests)]
[Trait("category", ServiceTestCategories.TradingTests)]
public sealed class TradingBarIntervalParserTests
{
    [Theory]
    [InlineData("1min", "1min", "1Min", 1)]
    [InlineData("5m", "5min", "5Min", 5)]
    [InlineData("15 Min", "15min", "15Min", 15)]
    [InlineData("1h", "1h", "1Hour", 60)]
    [InlineData("2hour", "2h", "2Hour", 120)]
    [InlineData("1d", "1d", "1Day", 1440)]
    [InlineData("3Day", "3d", "3Day", 4320)]
    public void TryParse_ReturnsExpectedInterval(
        string value,
        string canonical,
        string alpacaTimeframe,
        int durationMinutes
    )
    {
        var success = TradingBarIntervalParser.TryParse(value, out var interval);

        Assert.True(success);
        Assert.Equal(canonical, interval.Canonical);
        Assert.Equal(alpacaTimeframe, interval.AlpacaTimeframe);
        Assert.Equal(TimeSpan.FromMinutes(durationMinutes), interval.Duration);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("min")]
    [InlineData("0min")]
    [InlineData("-1min")]
    [InlineData("abc")]
    public void TryParse_ReturnsFalse_ForInvalidValue(string value)
    {
        var success = TradingBarIntervalParser.TryParse(value, out _);

        Assert.False(success);
    }
}
