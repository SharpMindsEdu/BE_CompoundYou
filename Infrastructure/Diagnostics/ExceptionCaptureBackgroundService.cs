using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Channels;
using Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Diagnostics;

public enum ExceptionCaptureKind
{
    FirstChance,
    Unhandled,
    UnobservedTask,
}

public sealed class ExceptionCaptureBackgroundService : BackgroundService
{
    private static readonly string[] AppNamespacePrefixes =
    [
        "Api.",
        "Application.",
        "Domain.",
        "Infrastructure.",
    ];

    private static readonly string[] IgnoredNamespacePrefixes =
    [
        "Infrastructure.Diagnostics.",
    ];

    private static readonly AsyncLocal<int> CaptureSuppressionDepth = new();

    private readonly Channel<ExceptionLogPayload> _queue;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<ExceptionCaptureBackgroundService> _logger;
    private long _droppedEntries;

    public ExceptionCaptureBackgroundService(
        IServiceScopeFactory serviceScopeFactory,
        IHttpContextAccessor httpContextAccessor,
        ILogger<ExceptionCaptureBackgroundService> logger
    )
    {
        _serviceScopeFactory = serviceScopeFactory;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
        _queue = Channel.CreateBounded<ExceptionLogPayload>(
            new BoundedChannelOptions(5_000)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false,
            }
        );
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        AppDomain.CurrentDomain.FirstChanceException += OnFirstChanceException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        _logger.LogInformation(
            "Exception capture activated (first-chance, unhandled, unobserved-task)."
        );

        return base.StartAsync(cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        AppDomain.CurrentDomain.FirstChanceException -= OnFirstChanceException;
        AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
        _queue.Writer.TryComplete();
        await base.StopAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var payload in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            await PersistAsync(payload, stoppingToken);
        }
    }

    private async Task PersistAsync(ExceptionLogPayload payload, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            using var _ = SuppressCapture();

            dbContext.ExceptionLogs.Add(payload.ToEntity());
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to persist exception log entry for {ExceptionType}.",
                payload.ExceptionType
            );
        }
    }

    private void OnFirstChanceException(object? sender, FirstChanceExceptionEventArgs args)
    {
        Capture(args.Exception, ExceptionCaptureKind.FirstChance);
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs args)
    {
        if (args.ExceptionObject is Exception ex)
            Capture(ex, ExceptionCaptureKind.Unhandled);
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs args)
    {
        Capture(args.Exception, ExceptionCaptureKind.UnobservedTask);
    }

    private void Capture(Exception exception, ExceptionCaptureKind kind)
    {
        if (CaptureSuppressionDepth.Value > 0)
            return;

        if (exception is OperationCanceledException)
            return;

        if (kind == ExceptionCaptureKind.FirstChance && !IsApplicationException(exception))
            return;

        if (!_queue.Writer.TryWrite(CreatePayload(exception, kind)))
        {
            var droppedEntries = Interlocked.Increment(ref _droppedEntries);
            if (droppedEntries == 1 || droppedEntries % 100 == 0)
            {
                _logger.LogWarning(
                    "Exception log queue is full. Dropped entries so far: {DroppedEntries}.",
                    droppedEntries
                );
            }
        }
    }

    private ExceptionLogPayload CreatePayload(Exception exception, ExceptionCaptureKind kind)
    {
        var context = _httpContextAccessor.HttpContext;
        var endpoint = context?.GetEndpoint()?.DisplayName;
        var queryString = context?.Request.QueryString.Value;
        var userIdentifier =
            context?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? context?.User.Identity?.Name;

        var metadata = new Dictionary<string, object?>
        {
            ["threadId"] = Environment.CurrentManagedThreadId,
            ["machineName"] = Environment.MachineName,
            ["endpoint"] = endpoint,
            ["queryString"] = queryString,
            ["isUserAuthenticated"] = context?.User.Identity?.IsAuthenticated == true,
            ["activityId"] = Activity.Current?.Id,
        };

        return new ExceptionLogPayload(
            DateTimeOffset.UtcNow,
            exception.GetType().FullName ?? exception.GetType().Name,
            Truncate(exception.Message, 4_000) ?? string.Empty,
            exception.StackTrace,
            Truncate(exception.Source, 512),
            kind.ToString(),
            kind == ExceptionCaptureKind.FirstChance,
            Truncate(context?.Request.Path.Value, 2_048),
            Truncate(context?.Request.Method, 16),
            Truncate(context?.TraceIdentifier, 128),
            Truncate(userIdentifier, 256),
            JsonSerializer.Serialize(metadata)
        );
    }

    private static bool IsApplicationException(Exception exception)
    {
        var declaringNamespace = exception.TargetSite?.DeclaringType?.Namespace;

        if (HasNamespacePrefix(declaringNamespace, IgnoredNamespacePrefixes))
            return false;

        if (HasNamespacePrefix(declaringNamespace, AppNamespacePrefixes))
            return true;

        var stackTrace = exception.StackTrace;
        if (string.IsNullOrWhiteSpace(stackTrace))
            return false;

        if (IgnoredNamespacePrefixes.Any(prefix => stackTrace.Contains(prefix, StringComparison.Ordinal)))
            return false;

        return AppNamespacePrefixes.Any(prefix => stackTrace.Contains(prefix, StringComparison.Ordinal));
    }

    private static bool HasNamespacePrefix(string? value, IEnumerable<string> prefixes)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return prefixes.Any(prefix => value.StartsWith(prefix, StringComparison.Ordinal));
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private static IDisposable SuppressCapture()
    {
        CaptureSuppressionDepth.Value++;
        return new CaptureScope();
    }

    private sealed class CaptureScope : IDisposable
    {
        public void Dispose()
        {
            CaptureSuppressionDepth.Value = Math.Max(0, CaptureSuppressionDepth.Value - 1);
        }
    }

    private sealed record ExceptionLogPayload(
        DateTimeOffset OccurredOnUtc,
        string ExceptionType,
        string Message,
        string? StackTrace,
        string? Source,
        string CaptureKind,
        bool IsHandled,
        string? RequestPath,
        string? RequestMethod,
        string? TraceId,
        string? UserIdentifier,
        string MetadataJson
    )
    {
        public ExceptionLog ToEntity() =>
            new()
            {
                OccurredOnUtc = OccurredOnUtc,
                ExceptionType = ExceptionType,
                Message = Message,
                StackTrace = StackTrace,
                Source = Source,
                CaptureKind = CaptureKind,
                IsHandled = IsHandled,
                RequestPath = RequestPath,
                RequestMethod = RequestMethod,
                TraceId = TraceId,
                UserIdentifier = UserIdentifier,
                MetadataJson = MetadataJson,
            };
    }
}
