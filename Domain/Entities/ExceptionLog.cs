namespace Domain.Entities;

public class ExceptionLog
{
    public long Id { get; set; }
    public DateTimeOffset OccurredOnUtc { get; set; } = DateTimeOffset.UtcNow;
    public required string ExceptionType { get; set; }
    public required string Message { get; set; }
    public string? StackTrace { get; set; }
    public string? Source { get; set; }
    public string? CaptureKind { get; set; }
    public bool IsHandled { get; set; }
    public string? RequestPath { get; set; }
    public string? RequestMethod { get; set; }
    public string? TraceId { get; set; }
    public string? UserIdentifier { get; set; }
    public string? MetadataJson { get; set; }
}
