using System.Text.Json.Serialization;
using Domain.Services.Trading;

namespace Application.Features.Trading.Automation;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DirectionalIndicatorMode
{
    Vwap,
    EmaCross,
    AdxDmi,
    SuperTrend,
}

/// <summary>
/// Settings that control which technical indicators are used to confirm
/// the trade direction before entry. Applied to both backtests and live trading.
/// </summary>
public sealed record DirectionalIndicatorSettings(
    bool Enabled = true,
    IReadOnlyList<DirectionalIndicatorMode>? Modes = null,
    bool RequireAll = true,
    int EmaShortPeriod = 9,
    int EmaLongPeriod = 20,
    int AdxPeriod = 14,
    decimal AdxThreshold = 25m,
    int SuperTrendAtrPeriod = 10,
    decimal SuperTrendFactor = 3m
)
{
    public static readonly IReadOnlyList<DirectionalIndicatorMode> DefaultModes =
        [DirectionalIndicatorMode.Vwap, DirectionalIndicatorMode.EmaCross];
}

/// <summary>
/// Evaluates whether the intraday session bars confirm a given trade direction.
///
/// Each enabled mode produces one boolean signal. <see cref="DirectionalIndicatorSettings.RequireAll"/>
/// controls AND vs OR aggregation. Modes that lack enough bars to compute are skipped (not counted as failures).
///
/// Modes:
///   Vwap       — price above/below session VWAP.
///   EmaCross   — short EMA above/below long EMA (configurable periods).
///   AdxDmi     — ADX below threshold → sidewalk, skip trade; otherwise +DI vs -DI for direction.
///   SuperTrend — ATR-based SuperTrend line determines current trend direction.
/// </summary>
public static class DirectionalIndicatorFilter
{
    public static bool IsConfirmed(
        TradingDirection direction,
        IReadOnlyList<TradingBarSnapshot> barsUpToEntry,
        DirectionalIndicatorSettings settings)
    {
        var modes = settings.Modes ?? DirectionalIndicatorSettings.DefaultModes;

        if (!settings.Enabled || modes.Count == 0)
            return true;

        if (barsUpToEntry.Count == 0)
            return true;

        var signals = new List<bool>(modes.Count);

        foreach (var mode in modes)
        {
            var signal = mode switch
            {
                DirectionalIndicatorMode.Vwap       => EvaluateVwap(direction, barsUpToEntry),
                DirectionalIndicatorMode.EmaCross    => EvaluateEmaCross(direction, barsUpToEntry, settings),
                DirectionalIndicatorMode.AdxDmi      => EvaluateAdxDmi(direction, barsUpToEntry, settings),
                DirectionalIndicatorMode.SuperTrend  => EvaluateSuperTrend(direction, barsUpToEntry, settings),
                _                                    => (bool?)null,
            };

            if (signal.HasValue)
                signals.Add(signal.Value);
        }

        if (signals.Count == 0)
            return true;

        return settings.RequireAll ? signals.All(x => x) : signals.Any(x => x);
    }

    // ── VWAP ────────────────────────────────────────────────────────────────

    private static bool? EvaluateVwap(TradingDirection direction, IReadOnlyList<TradingBarSnapshot> bars)
    {
        var vwap = ComputeVwap(bars);
        if (vwap <= 0m) return null;

        var price = bars[^1].Close;
        return direction switch
        {
            TradingDirection.Bullish => price > vwap,
            TradingDirection.Bearish => price < vwap,
            _                        => true,
        };
    }

    private static decimal ComputeVwap(IReadOnlyList<TradingBarSnapshot> bars)
    {
        var totalVolume = 0m;
        var sumTpv = 0m;

        foreach (var bar in bars)
        {
            if (bar.Volume <= 0m) continue;
            var typicalPrice = (bar.High + bar.Low + bar.Close) / 3m;
            sumTpv += typicalPrice * bar.Volume;
            totalVolume += bar.Volume;
        }

        return totalVolume > 0m ? sumTpv / totalVolume : 0m;
    }

    // ── EMA Cross ───────────────────────────────────────────────────────────

    private static bool? EvaluateEmaCross(
        TradingDirection direction,
        IReadOnlyList<TradingBarSnapshot> bars,
        DirectionalIndicatorSettings settings)
    {
        var shortPeriod = Math.Max(2, settings.EmaShortPeriod);
        var longPeriod  = Math.Max(shortPeriod + 1, settings.EmaLongPeriod);

        if (bars.Count < longPeriod) return null;

        var closes   = bars.Select(b => b.Close).ToList();
        var shortEma = ComputeEma(closes, shortPeriod);
        var longEma  = ComputeEma(closes, longPeriod);

        return direction switch
        {
            TradingDirection.Bullish => shortEma > longEma,
            TradingDirection.Bearish => shortEma < longEma,
            _                        => true,
        };
    }

    private static decimal ComputeEma(IReadOnlyList<decimal> closes, int period)
    {
        if (closes.Count < period) return closes[^1];

        var multiplier = 2m / (period + 1m);
        var ema = 0m;
        for (var i = 0; i < period; i++)
            ema += closes[i];
        ema /= period;

        for (var i = period; i < closes.Count; i++)
            ema = closes[i] * multiplier + ema * (1m - multiplier);

        return ema;
    }

    // ── ADX + DMI ───────────────────────────────────────────────────────────
    // ADX measures trend strength (0–100). Below AdxThreshold = ranging/sidewalk → skip trade.
    // +DI > -DI = bullish trend; -DI > +DI = bearish trend.
    // Requires 2 * period bars for a meaningful ADX value.

    private static bool? EvaluateAdxDmi(
        TradingDirection direction,
        IReadOnlyList<TradingBarSnapshot> bars,
        DirectionalIndicatorSettings settings)
    {
        var period = Math.Max(2, settings.AdxPeriod);
        if (bars.Count < 2 * period) return null;

        var (adx, plusDi, minusDi) = ComputeAdxDmi(bars, period);

        if (adx < settings.AdxThreshold) return false;

        return direction switch
        {
            TradingDirection.Bullish => plusDi > minusDi,
            TradingDirection.Bearish => minusDi > plusDi,
            _                        => true,
        };
    }

    private static (decimal adx, decimal plusDi, decimal minusDi) ComputeAdxDmi(
        IReadOnlyList<TradingBarSnapshot> bars, int period)
    {
        var n        = bars.Count;
        var trArr    = new decimal[n - 1];
        var plusDmArr  = new decimal[n - 1];
        var minusDmArr = new decimal[n - 1];

        for (var i = 1; i < n; i++)
        {
            var curr   = bars[i];
            var prev   = bars[i - 1];
            var upMove = curr.High - prev.High;
            var dnMove = prev.Low  - curr.Low;

            trArr[i - 1]    = Math.Max(curr.High - curr.Low,
                              Math.Max(Math.Abs(curr.High - prev.Close),
                                       Math.Abs(curr.Low  - prev.Close)));
            plusDmArr[i - 1]  = upMove > dnMove && upMove > 0m ? upMove : 0m;
            minusDmArr[i - 1] = dnMove > upMove && dnMove > 0m ? dnMove : 0m;
        }

        if (trArr.Length < period) return (0m, 0m, 0m);

        // Wilder initial smooth: sum of first `period` raw values
        var smoothTr       = trArr.Take(period).Sum();
        var smoothPlusDm   = plusDmArr.Take(period).Sum();
        var smoothMinusDm  = minusDmArr.Take(period).Sum();

        var dxList = new List<decimal>(trArr.Length - period + 1);
        AppendDx(smoothTr, smoothPlusDm, smoothMinusDm, dxList);

        for (var i = period; i < trArr.Length; i++)
        {
            smoothTr      = smoothTr      - smoothTr      / period + trArr[i];
            smoothPlusDm  = smoothPlusDm  - smoothPlusDm  / period + plusDmArr[i];
            smoothMinusDm = smoothMinusDm - smoothMinusDm / period + minusDmArr[i];
            AppendDx(smoothTr, smoothPlusDm, smoothMinusDm, dxList);
        }

        if (dxList.Count == 0) return (0m, 0m, 0m);

        // ADX = Wilder smoothing of DX series
        decimal adx;
        if (dxList.Count < period)
        {
            adx = dxList.Average();
        }
        else
        {
            adx = dxList.Take(period).Average();
            for (var i = period; i < dxList.Count; i++)
                adx = (adx * (period - 1) + dxList[i]) / period;
        }

        var finalPlusDi  = smoothTr > 0m ? 100m * smoothPlusDm  / smoothTr : 0m;
        var finalMinusDi = smoothTr > 0m ? 100m * smoothMinusDm / smoothTr : 0m;

        return (adx, finalPlusDi, finalMinusDi);
    }

    private static void AppendDx(
        decimal smoothTr, decimal smoothPlusDm, decimal smoothMinusDm, List<decimal> dxList)
    {
        if (smoothTr <= 0m) return;
        var plusDi  = 100m * smoothPlusDm  / smoothTr;
        var minusDi = 100m * smoothMinusDm / smoothTr;
        var diSum   = plusDi + minusDi;
        if (diSum > 0m)
            dxList.Add(100m * Math.Abs(plusDi - minusDi) / diSum);
    }

    // ── SuperTrend ──────────────────────────────────────────────────────────
    // ATR-based trend-following indicator. Tracks an upper and lower band;
    // direction flips when price crosses the active band.
    // Requires period + 2 bars (period for ATR + 1 initialisation bar + 1 first direction bar).

    private static bool? EvaluateSuperTrend(
        TradingDirection direction,
        IReadOnlyList<TradingBarSnapshot> bars,
        DirectionalIndicatorSettings settings)
    {
        var period = Math.Max(1, settings.SuperTrendAtrPeriod);
        if (bars.Count < period + 2) return null;

        var trendDir = ComputeSuperTrendDirection(bars, period, settings.SuperTrendFactor);
        if (trendDir == 0) return null;

        return direction switch
        {
            TradingDirection.Bullish => trendDir == 1,
            TradingDirection.Bearish => trendDir == -1,
            _                        => true,
        };
    }

    private static int ComputeSuperTrendDirection(
        IReadOnlyList<TradingBarSnapshot> bars, int period, decimal factor)
    {
        var count = bars.Count;
        if (count < period + 2) return 0;

        // True Range (needs prev bar, starts at index 1)
        var tr = new decimal[count];
        for (var i = 1; i < count; i++)
        {
            var curr = bars[i];
            var prev = bars[i - 1];
            tr[i] = Math.Max(curr.High - curr.Low,
                    Math.Max(Math.Abs(curr.High - prev.Close),
                             Math.Abs(curr.Low  - prev.Close)));
        }

        // Wilder ATR: atr[period] = average of TR[1..period]; atr[i] = Wilder smooth thereafter
        var atr = new decimal[count];
        for (var i = 1; i <= period; i++)
            atr[period] += tr[i];
        atr[period] /= period;
        for (var i = period + 1; i < count; i++)
            atr[i] = (atr[i - 1] * (period - 1) + tr[i]) / period;

        // Initialise bands at bar `period`
        var hl2_init = (bars[period].High + bars[period].Low) / 2m;
        var fub = hl2_init + factor * atr[period]; // final upper band
        var flb = hl2_init - factor * atr[period]; // final lower band
        var dir = 1; // 1 = bullish (tracking lower band), -1 = bearish (tracking upper band)

        for (var i = period + 1; i < count; i++)
        {
            var bar      = bars[i];
            var prevClose = bars[i - 1].Close;
            var hl2      = (bar.High + bar.Low) / 2m;
            var basicUb  = hl2 + factor * atr[i];
            var basicLb  = hl2 - factor * atr[i];

            var prevFub = fub;
            var prevFlb = flb;

            // Band only tightens inward; resets if prev close crossed it
            fub = basicUb < prevFub || prevClose > prevFub ? basicUb : prevFub;
            flb = basicLb > prevFlb || prevClose < prevFlb ? basicLb : prevFlb;

            dir = dir == -1
                ? (bar.Close > fub ? 1 : -1)
                : (bar.Close < flb ? -1 : 1);
        }

        return dir;
    }
}
