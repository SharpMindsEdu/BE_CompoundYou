using Infrastructure.Hubs;
using Application.Shared.Services;
using Microsoft.AspNetCore.SignalR;

namespace Infrastructure.Services;

public class MatrixNotificationService(IHubContext<MatrixHub> hubContext) : IMatrixNotificationService
{
    public async Task NotifyAssessmentValidatedAsync(long assessmentId, long employeeId, long? teamId, CancellationToken ct = default)
    {
        // Notify the employee directly
        // Note: Using ToString() assuming SignalR UserId maps to our UserId
        await hubContext.Clients.User(employeeId.ToString())
            .SendAsync(MatrixHub.AssessmentValidatedEvent, assessmentId, ct);

        // Notify the team group if applicable
        if (teamId.HasValue)
        {
            await hubContext.Clients.Group($"Team_{teamId.Value}")
                .SendAsync(MatrixHub.AssessmentValidatedEvent, assessmentId, ct);
        }
    }
}
