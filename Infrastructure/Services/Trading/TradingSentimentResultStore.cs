using System.Text.Json;
using Application.Features.Trading.Live;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services.Trading;

public sealed class TradingSentimentResultStore : ITradingSentimentResultStore
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly object _latestLock = new();
    private readonly ILogger<TradingSentimentResultStore> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private volatile TradingSentimentAnalysisResult? _latest;

    public TradingSentimentResultStore(
        IServiceScopeFactory scopeFactory,
        ILogger<TradingSentimentResultStore> logger
    )
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<long?> SetLatestAsync(
        TradingSentimentAnalysisResult result,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(result);

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var record = new TradingSentimentAnalysisRecord
            {
                AnalyzedAtUtc = result.AnalyzedAtUtc,
                TradingDate = result.TradingDate,
                AgentText = string.IsNullOrWhiteSpace(result.AgentText) ? null : result.AgentText,
                AllOpportunitiesJson = JsonSerializer.Serialize(
                    result.AllOpportunities,
                    JsonSerializerOptions
                ),
                CreatedOn = DateTimeOffset.UtcNow,
                UpdatedOn = DateTimeOffset.UtcNow,
                DeletedOn = DateTimeOffset.MinValue,
            };

            dbContext.TradingSentimentAnalyses.Add(record);
            await dbContext.SaveChangesAsync(cancellationToken);

            Volatile.Write(ref _latest, result);
            return record.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist trading sentiment analysis result.");
            return null;
        }
    }

    public TradingSentimentAnalysisResult? GetLatest()
    {
        var cached = Volatile.Read(ref _latest);
        if (cached is not null)
        {
            return cached;
        }

        lock (_latestLock)
        {
            cached = Volatile.Read(ref _latest);
            if (cached is not null)
            {
                return cached;
            }

            var loaded = LoadLatestFromDatabase();
            Volatile.Write(ref _latest, loaded);
            return loaded;
        }
    }

    public TradingSentimentAnalysisResult? GetById(long id)
    {
        if (id <= 0)
        {
            return null;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var record = dbContext.TradingSentimentAnalyses
                .AsNoTracking()
                .FirstOrDefault(x => x.Id == id);
            return record is null ? null : MapRecord(record);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load sentiment analysis {AnalysisId} from database.", id);
            return null;
        }
    }

    private TradingSentimentAnalysisResult? LoadLatestFromDatabase()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var record = dbContext.TradingSentimentAnalyses
                .AsNoTracking()
                .OrderByDescending(x => x.AnalyzedAtUtc)
                .ThenByDescending(x => x.Id)
                .FirstOrDefault();
            return record is null ? null : MapRecord(record);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load latest sentiment analysis from database.");
            return null;
        }
    }

    private static TradingSentimentAnalysisResult MapRecord(TradingSentimentAnalysisRecord record)
    {
        var opportunities = DeserializeOpportunities(record.AllOpportunitiesJson);
        return new TradingSentimentAnalysisResult(
            record.AnalyzedAtUtc,
            record.TradingDate,
            record.AgentText,
            opportunities
        );
    }

    private static IReadOnlyCollection<TradingSentimentOpportunityResult> DeserializeOpportunities(
        string? json
    )
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<TradingSentimentOpportunityResult>();
        }

        try
        {
            return JsonSerializer.Deserialize<TradingSentimentOpportunityResult[]>(
                       json,
                       JsonSerializerOptions
                   )
                   ?? Array.Empty<TradingSentimentOpportunityResult>();
        }
        catch (JsonException)
        {
            return Array.Empty<TradingSentimentOpportunityResult>();
        }
    }
}
