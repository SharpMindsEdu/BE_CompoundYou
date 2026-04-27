using Application.Features.Trading.Live;

namespace Infrastructure.Services.Trading;

public sealed class PreMarketScanTrigger : IPreMarketScanTrigger
{
    private int _requested;

    public void RequestScan()
    {
        Interlocked.Exchange(ref _requested, 1);
    }

    public bool TryConsume()
    {
        return Interlocked.Exchange(ref _requested, 0) == 1;
    }
}
