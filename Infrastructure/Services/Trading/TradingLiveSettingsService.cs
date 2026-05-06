using Application.Features.Trading.Automation;
using Application.Features.Trading.LiveSettings;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services.Trading;

public sealed class TradingLiveSettingsService(
    ApplicationDbContext db,
    IOptions<TradingAutomationOptions> configOptions
) : ITradingLiveSettingsService
{
    public async Task<TradingLiveSettingsDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var settings = await db.TradingLiveSettings.FirstOrDefaultAsync(cancellationToken);
        var defaults = BuildConfigDefaults(configOptions.Value);
        return MapToDto(settings, defaults);
    }

    public async Task<TradingLiveSettingsDto> UpdateAsync(
        UpdateTradingLiveSettingsRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var settings = await db.TradingLiveSettings.FirstOrDefaultAsync(cancellationToken);

        if (settings is null)
        {
            settings = new TradingLiveSettings();
            db.TradingLiveSettings.Add(settings);
        }

        settings.MinOpportunities = request.MinOpportunities;
        settings.MaxOpportunities = request.MaxOpportunities;
        settings.MinimumSentimentScore = request.MinimumSentimentScore;
        settings.MinimumRetestScore = request.MinimumRetestScore;
        settings.MinimumMinutesFromMarketOpenForEntry = request.MinimumMinutesFromMarketOpenForEntry;
        settings.MaximumMinutesFromMarketOpenForEntry = request.MaximumMinutesFromMarketOpenForEntry;
        settings.MinimumEntryDistanceFromRangeFraction = request.MinimumEntryDistanceFromRangeFraction;
        settings.MaxMinutesBreakoutToRetest = request.MaxMinutesBreakoutToRetest;
        settings.StopLossBufferFraction = request.StopLossBufferFraction;
        settings.RewardToRiskRatio = request.RewardToRiskRatio;
        settings.OrderQuantity = request.OrderQuantity;
        settings.RiskPerTradeFraction = request.RiskPerTradeFraction;
        settings.BreakEvenAtRMultiple = request.BreakEvenAtRMultiple;
        settings.MaxBarsInTradeBeforeFlatExit = request.MaxBarsInTradeBeforeFlatExit;
        settings.MaxTradesPerDay = request.MaxTradesPerDay;
        settings.MaxDailyLossFraction = request.MaxDailyLossFraction;
        settings.UseTrailingStopLoss = request.UseTrailingStopLoss;
        settings.PartialTakeProfitFraction = request.PartialTakeProfitFraction;
        settings.TrailingStopRiskMultiple = request.TrailingStopRiskMultiple;
        settings.TrailingStopBreakEvenProtection = request.TrailingStopBreakEvenProtection;
        settings.UseRetestValidationAgent = request.UseRetestValidationAgent;
        settings.UseDirectionalIndicatorFilter = request.UseDirectionalIndicatorFilter;
        settings.DirectionalIndicatorRequireAll = request.DirectionalIndicatorRequireAll;
        settings.DirectionalIndicatorModesJson = request.DirectionalIndicatorModes is not null
            ? System.Text.Json.JsonSerializer.Serialize(request.DirectionalIndicatorModes)
            : null;
        settings.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        var defaults = BuildConfigDefaults(configOptions.Value);
        return MapToDto(settings, defaults);
    }

    internal static TradingLiveSettingsConfigDefaults BuildConfigDefaults(TradingAutomationOptions opts) =>
        new(
            MinOpportunities: opts.MinOpportunities,
            MaxOpportunities: opts.MaxOpportunities,
            MinimumSentimentScore: opts.MinimumSentimentScore,
            MinimumRetestScore: opts.MinimumRetestScore,
            MinimumMinutesFromMarketOpenForEntry: opts.MinimumMinutesFromMarketOpenForEntry,
            MaximumMinutesFromMarketOpenForEntry: opts.MaximumMinutesFromMarketOpenForEntry,
            MinimumEntryDistanceFromRangeFraction: opts.MinimumEntryDistanceFromRangeFraction,
            MaxMinutesBreakoutToRetest: opts.MaxMinutesBreakoutToRetest,
            StopLossBufferFraction: opts.StopLossBufferFraction,
            RewardToRiskRatio: opts.RewardToRiskRatio,
            OrderQuantity: opts.OrderQuantity,
            RiskPerTradeFraction: opts.RiskPerTradeFraction,
            BreakEvenAtRMultiple: opts.BreakEvenAtRMultiple,
            MaxBarsInTradeBeforeFlatExit: opts.MaxBarsInTradeBeforeFlatExit,
            MaxTradesPerDay: opts.MaxTradesPerDay,
            MaxDailyLossFraction: opts.MaxDailyLossFraction,
            UseTrailingStopLoss: opts.LiveUseTrailingStopLoss,
            PartialTakeProfitFraction: opts.LivePartialTakeProfitFraction,
            TrailingStopRiskMultiple: opts.LiveTrailingStopRiskMultiple,
            TrailingStopBreakEvenProtection: opts.LiveTrailingStopBreakEvenProtection,
            UseRetestValidationAgent: opts.UseRetestValidationAgent,
            UseDirectionalIndicatorFilter: opts.UseDirectionalIndicatorFilter,
            DirectionalIndicatorRequireAll: opts.DirectionalIndicatorRequireAll,
            DirectionalIndicatorModes: opts.DirectionalIndicatorModes
        );

    private static TradingLiveSettingsDto MapToDto(
        TradingLiveSettings? settings,
        TradingLiveSettingsConfigDefaults defaults
    )
    {
        IReadOnlyList<DirectionalIndicatorMode>? modes = null;
        if (settings?.DirectionalIndicatorModesJson is not null)
        {
            modes = System.Text.Json.JsonSerializer.Deserialize<List<DirectionalIndicatorMode>>(
                settings.DirectionalIndicatorModesJson
            );
        }

        return new TradingLiveSettingsDto(
            ConfigDefaults: defaults,
            MinOpportunities: settings?.MinOpportunities,
            MaxOpportunities: settings?.MaxOpportunities,
            MinimumSentimentScore: settings?.MinimumSentimentScore,
            MinimumRetestScore: settings?.MinimumRetestScore,
            MinimumMinutesFromMarketOpenForEntry: settings?.MinimumMinutesFromMarketOpenForEntry,
            MaximumMinutesFromMarketOpenForEntry: settings?.MaximumMinutesFromMarketOpenForEntry,
            MinimumEntryDistanceFromRangeFraction: settings?.MinimumEntryDistanceFromRangeFraction,
            MaxMinutesBreakoutToRetest: settings?.MaxMinutesBreakoutToRetest,
            StopLossBufferFraction: settings?.StopLossBufferFraction,
            RewardToRiskRatio: settings?.RewardToRiskRatio,
            OrderQuantity: settings?.OrderQuantity,
            RiskPerTradeFraction: settings?.RiskPerTradeFraction,
            BreakEvenAtRMultiple: settings?.BreakEvenAtRMultiple,
            MaxBarsInTradeBeforeFlatExit: settings?.MaxBarsInTradeBeforeFlatExit,
            MaxTradesPerDay: settings?.MaxTradesPerDay,
            MaxDailyLossFraction: settings?.MaxDailyLossFraction,
            UseTrailingStopLoss: settings?.UseTrailingStopLoss,
            PartialTakeProfitFraction: settings?.PartialTakeProfitFraction,
            TrailingStopRiskMultiple: settings?.TrailingStopRiskMultiple,
            TrailingStopBreakEvenProtection: settings?.TrailingStopBreakEvenProtection,
            UseRetestValidationAgent: settings?.UseRetestValidationAgent,
            UseDirectionalIndicatorFilter: settings?.UseDirectionalIndicatorFilter,
            DirectionalIndicatorRequireAll: settings?.DirectionalIndicatorRequireAll,
            DirectionalIndicatorModes: modes
        );
    }
}
