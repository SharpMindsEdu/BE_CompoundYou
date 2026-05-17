namespace Application.Shared.Services;

public interface IMatrixNotificationService
{
    Task NotifyAssessmentValidatedAsync(long assessmentId, long employeeId, long? teamId, CancellationToken ct = default);
}
