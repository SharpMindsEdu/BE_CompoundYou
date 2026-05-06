namespace Application.Features.Trading.Automation;

/// <summary>
/// Shared position-sizing helpers used by both backtest and live trading paths.
/// </summary>
public static class PositionSizing
{
    /// <summary>
    /// Rounds a configured fixed quantity. Used as the fallback when risk-based
    /// sizing is not enabled.
    /// </summary>
    public static decimal RoundConfiguredQuantity(decimal quantity, bool useWholeShareQuantity)
    {
        if (quantity <= 0m)
        {
            return 0m;
        }

        return useWholeShareQuantity
            ? decimal.Floor(quantity)
            : decimal.Round(quantity, 6, MidpointRounding.ToZero);
    }

    /// <summary>
    /// Computes risk-based position size. When <paramref name="riskPerTradeFraction"/>
    /// is &gt; 0 and <paramref name="riskPerUnit"/> is positive, returns
    /// floor(equity * fraction / riskPerUnit). Otherwise falls back to
    /// <paramref name="configuredQuantity"/>.
    /// </summary>
    public static decimal Resolve(
        decimal configuredQuantity,
        decimal accountEquity,
        decimal riskPerUnit,
        decimal riskPerTradeFraction,
        bool useWholeShareQuantity
    )
    {
        if (riskPerTradeFraction > 0m && riskPerUnit > 0m && accountEquity > 0m)
        {
            var dollarRisk = accountEquity * riskPerTradeFraction;
            var rawQuantity = dollarRisk / riskPerUnit;
            return useWholeShareQuantity
                ? decimal.Floor(rawQuantity)
                : decimal.Round(rawQuantity, 6, MidpointRounding.ToZero);
        }

        return RoundConfiguredQuantity(configuredQuantity, useWholeShareQuantity);
    }
}
