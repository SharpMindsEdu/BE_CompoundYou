using Microsoft.AspNetCore.SignalR;

namespace Infrastructure.Hubs;

public class MatrixHub : Hub
{
    public const string AssessmentValidatedEvent = "AssessmentValidated";

    public async Task JoinTeamGroup(long teamId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"Team_{teamId}");
    }

    public async Task LeaveTeamGroup(long teamId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Team_{teamId}");
    }
}
