using Application.Features.Trading.Live;

namespace Infrastructure.Services.Trading;

public sealed class TradingSentimentResultStore : ITradingSentimentResultStore
{
    private TradingSentimentAnalysisResult? _latest;

    public void SetLatest(TradingSentimentAnalysisResult result)
    {
        Volatile.Write(ref _latest, result);
    }

    public TradingSentimentAnalysisResult? GetLatest()
    {
        return Volatile.Read(ref _latest);
    }
}
