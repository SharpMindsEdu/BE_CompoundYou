namespace Application.Features.Trading.LiveSettings;

public interface ITradingLiveSettingsService
{
    Task<TradingLiveSettingsDto> GetAsync(CancellationToken cancellationToken = default);
    Task<TradingLiveSettingsDto> UpdateAsync(
        UpdateTradingLiveSettingsRequest request,
        CancellationToken cancellationToken = default
    );
}
