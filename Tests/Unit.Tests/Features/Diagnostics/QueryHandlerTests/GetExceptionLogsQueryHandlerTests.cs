using Application.Features.Diagnostics.Queries;
using Application.Shared;
using Domain.Entities;
using Unit.Tests.Features.Base;

namespace Unit.Tests.Features.Diagnostics.QueryHandlerTests;

[Trait("category", ServiceTestCategories.UnitTests)]
public sealed class GetExceptionLogsQueryHandlerTests(
    PostgreSqlRepositoryTestDatabaseFixture fixture,
    ITestOutputHelper outputHelper
) : FeatureTestBase(fixture, outputHelper)
{
    [Fact]
    public async Task GetExceptionLogs_WithSearchAndHandledFilter_ReturnsMatchingRows()
    {
        var now = new DateTimeOffset(2026, 4, 23, 13, 0, 0, TimeSpan.Zero);
        PersistWithDatabase(db =>
            db.AddRange(
                BuildExceptionLog(
                    "System.InvalidOperationException",
                    "The requested operation is invalid.",
                    now.AddMinutes(-20),
                    isHandled: true
                ),
                BuildExceptionLog(
                    "System.NullReferenceException",
                    "Object reference not set to an instance of an object.",
                    now.AddMinutes(-10),
                    isHandled: false
                ),
                BuildExceptionLog(
                    "System.TimeoutException",
                    "Operation timed out.",
                    now.AddMinutes(-5),
                    isHandled: false
                )
            )
        );

        var result = await Send(
            new GetExceptionLogs.Query(Search: "nullreference", IsHandled: false, PageSize: 25),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data!.Items);
        Assert.Contains("NullReferenceException", result.Data.Items.First().ExceptionType);
    }

    [Fact]
    public async Task GetExceptionLogById_WithUnknownId_ReturnsNotFoundResult()
    {
        var result = await Send(
            new GetExceptionLogById.Query(888_888),
            TestContext.Current.CancellationToken
        );

        Assert.False(result.Succeeded);
        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task GetExceptionLogsSummary_ReturnsTopExceptionTypes()
    {
        var now = new DateTimeOffset(2026, 4, 23, 14, 0, 0, TimeSpan.Zero);
        PersistWithDatabase(db =>
            db.AddRange(
                BuildExceptionLog("System.TimeoutException", "Timeout #1", now.AddMinutes(-30), true),
                BuildExceptionLog("System.TimeoutException", "Timeout #2", now.AddMinutes(-20), false),
                BuildExceptionLog(
                    "System.InvalidOperationException",
                    "Invalid operation",
                    now.AddMinutes(-10),
                    false
                )
            )
        );

        var result = await Send(
            new GetExceptionLogsSummary.Query(),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Data);
        Assert.Equal(3, result.Data!.TotalExceptions);
        Assert.Equal(1, result.Data.HandledExceptions);
        Assert.Equal(2, result.Data.UnhandledExceptions);
        Assert.Equal(2, result.Data.DistinctExceptionTypes);
        Assert.NotEmpty(result.Data.TopExceptionTypes);
        Assert.Equal("System.TimeoutException", result.Data.TopExceptionTypes.First().ExceptionType);
        Assert.Equal(2, result.Data.TopExceptionTypes.First().Count);
    }

    private static ExceptionLog BuildExceptionLog(
        string exceptionType,
        string message,
        DateTimeOffset occurredOnUtc,
        bool isHandled
    )
    {
        return new ExceptionLog
        {
            ExceptionType = exceptionType,
            Message = message,
            OccurredOnUtc = occurredOnUtc,
            IsHandled = isHandled,
            RequestPath = "/api/test",
            RequestMethod = "GET",
            TraceId = Guid.NewGuid().ToString("N"),
            UserIdentifier = "test-user",
            CaptureKind = "GlobalExceptionMiddleware",
        };
    }
}
