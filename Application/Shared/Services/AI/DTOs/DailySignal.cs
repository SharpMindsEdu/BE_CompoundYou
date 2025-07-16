using System.Globalization;

namespace Application.Shared.Services.AI.DTOs;

public class DailySignal
{
    public string  Symbol   { get; init; } = default!;
    public string  Direction   { get; init; } = default!; // "buy" oder "sell"
    public decimal EntryPrice { get; init; }
    public decimal TakeProfit { get; init; }
    public decimal StopLoss   { get; init; }
    public int     Confidence  { get; init; }  // 0–100
    public string  Rationale   { get; init; } = default!;

    public string ToCommand()
    {
        var direction = Direction == "buy" ? "0" : "1";
        return $"{direction} {Symbol} 0.01 {TakeProfit.ToString(CultureInfo.InvariantCulture)} {StopLoss.ToString(CultureInfo.InvariantCulture)}";
    }
}
