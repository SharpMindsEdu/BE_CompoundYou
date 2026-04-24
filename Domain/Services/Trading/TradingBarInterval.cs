using System.Globalization;
using System.Text.RegularExpressions;

namespace Domain.Services.Trading;

public readonly record struct TradingBarInterval(
    string Canonical,
    string AlpacaTimeframe,
    TimeSpan Duration
)
{
    public override string ToString()
    {
        return Canonical;
    }
}

public static partial class TradingBarIntervalParser
{
    private const int MaxIntervalQuantity = 10_000;

    public static bool TryParse(string? value, out TradingBarInterval interval)
    {
        interval = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim();
        var compact = string.Concat(normalized.Where(ch => !char.IsWhiteSpace(ch)));

        if (TryParseAlpacaTimeframe(compact, out interval))
        {
            return true;
        }

        var match = FlexibleIntervalRegex().Match(compact);
        if (!match.Success)
        {
            return false;
        }

        if (!int.TryParse(match.Groups["amount"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var amount))
        {
            return false;
        }

        if (amount <= 0 || amount > MaxIntervalQuantity)
        {
            return false;
        }

        var unit = match.Groups["unit"].Value.ToLowerInvariant();
        return TryBuildInterval(amount, unit, out interval);
    }

    public static TradingBarInterval Parse(string value)
    {
        if (!TryParse(value, out var interval))
        {
            throw new ArgumentException(
                "Interval must be a positive value such as '1min', '5min', '15min', '1h', or '1d'.",
                nameof(value)
            );
        }

        return interval;
    }

    private static bool TryParseAlpacaTimeframe(string value, out TradingBarInterval interval)
    {
        interval = default;
        var match = AlpacaIntervalRegex().Match(value);
        if (!match.Success)
        {
            return false;
        }

        if (!int.TryParse(match.Groups["amount"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var amount))
        {
            return false;
        }

        if (amount <= 0 || amount > MaxIntervalQuantity)
        {
            return false;
        }

        var unit = match.Groups["unit"].Value.ToLowerInvariant();
        return TryBuildInterval(amount, unit, out interval);
    }

    private static bool TryBuildInterval(int amount, string unit, out TradingBarInterval interval)
    {
        interval = default;

        if (unit is "m" or "min" or "mins" or "minute" or "minutes")
        {
            interval = new TradingBarInterval(
                $"{amount}min",
                $"{amount}Min",
                TimeSpan.FromMinutes(amount)
            );
            return true;
        }

        if (unit is "h" or "hr" or "hrs" or "hour" or "hours")
        {
            interval = new TradingBarInterval(
                $"{amount}h",
                $"{amount}Hour",
                TimeSpan.FromHours(amount)
            );
            return true;
        }

        if (unit is "d" or "day" or "days")
        {
            interval = new TradingBarInterval(
                $"{amount}d",
                $"{amount}Day",
                TimeSpan.FromDays(amount)
            );
            return true;
        }

        return false;
    }

    [GeneratedRegex(
        "^(?<amount>\\d+)(?<unit>m|min|mins|minute|minutes|h|hr|hrs|hour|hours|d|day|days)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    )]
    private static partial Regex FlexibleIntervalRegex();

    [GeneratedRegex(
        "^(?<amount>\\d+)(?<unit>Min|Hour|Day)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    )]
    private static partial Regex AlpacaIntervalRegex();
}
