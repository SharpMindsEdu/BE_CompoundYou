using Application.Shared.Services.AI.DTOs;

namespace Application.Shared.Services.AI;

public interface IAiService
{
    public Task<DailySignal?> GetDailySignalAsync(string symbol, decimal fxQuote);
}