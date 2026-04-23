using System.Text.Json;
using Application.Features.Trading.Automation;
using Domain.Services.Trading;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services.Trading;

public interface ITradingAutomationStateStore
{
    Task<TradingAutomationStateSnapshot?> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(
        TradingAutomationStateSnapshot snapshot,
        CancellationToken cancellationToken = default
    );
}

public sealed record TradingAutomationStateSnapshot(
    DateOnly TradingDate,
    DateOnly? LastSentimentScanDate,
    IReadOnlyCollection<TradingAutomationSymbolStateSnapshot> Symbols
);

public sealed record TradingAutomationSymbolStateSnapshot(
    TradingOpportunity Opportunity,
    OpeningRangeSnapshot? OpeningRange,
    TradingBarSnapshot? BreakoutBar,
    DateTimeOffset? LastEvaluatedRetestTimestamp,
    bool OrderPlaced,
    string? OrderId,
    DateTimeOffset? OrderSubmittedAtUtc,
    DateTimeOffset? EntrySignalBarTimestampUtc,
    decimal? PlannedEntryPrice,
    decimal? StopLossPrice,
    decimal? TakeProfitPrice,
    bool EntryAuditLogged,
    bool ExitAuditLogged,
    DateTimeOffset? EntryFilledAtUtc,
    DateTimeOffset? ExitFilledAtUtc,
    DateTimeOffset? EntryBarTimestampUtc,
    DateTimeOffset? ExitBarTimestampUtc,
    int? EntryBarIndex,
    int? ExitBarIndex,
    bool OrderSubmissionRejected,
    string? LastOrderSubmissionError,
    DateTimeOffset? LastOrderSubmissionFailedAtUtc,
    string? TradedInstrumentSymbol,
    string? OptionContractType,
    decimal? OptionStrikePrice,
    DateOnly? OptionExpirationDate,
    string? PendingExitOrderId,
    string? PendingExitReason
);

public sealed class FileTradingAutomationStateStore : ITradingAutomationStateStore
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly ILogger<FileTradingAutomationStateStore> _logger;
    private readonly SemaphoreSlim _mutex = new(1, 1);
    private readonly string _stateFilePath;

    public FileTradingAutomationStateStore(
        IOptions<TradingAutomationOptions> options,
        IHostEnvironment hostEnvironment,
        ILogger<FileTradingAutomationStateStore> logger
    )
    {
        _logger = logger;

        var configuredPath = options.Value.StateFilePath?.Trim();
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            configuredPath = "artifacts/trading-automation-state.json";
        }

        _stateFilePath = Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.GetFullPath(Path.Combine(hostEnvironment.ContentRootPath, configuredPath));
    }

    public async Task<TradingAutomationStateSnapshot?> LoadAsync(
        CancellationToken cancellationToken = default
    )
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(_stateFilePath))
            {
                return null;
            }

            await using var stream = File.OpenRead(_stateFilePath);
            return await JsonSerializer.DeserializeAsync<TradingAutomationStateSnapshot>(
                stream,
                JsonSerializerOptions,
                cancellationToken
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to load trading automation state file at {Path}.",
                _stateFilePath
            );
            return null;
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task SaveAsync(
        TradingAutomationStateSnapshot snapshot,
        CancellationToken cancellationToken = default
    )
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            var parent = Path.GetDirectoryName(_stateFilePath);
            if (!string.IsNullOrWhiteSpace(parent))
            {
                Directory.CreateDirectory(parent);
            }

            await using var stream = new FileStream(
                _stateFilePath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None
            );
            await JsonSerializer.SerializeAsync(
                stream,
                snapshot,
                JsonSerializerOptions,
                cancellationToken
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to save trading automation state file at {Path}.",
                _stateFilePath
            );
        }
        finally
        {
            _mutex.Release();
        }
    }
}
