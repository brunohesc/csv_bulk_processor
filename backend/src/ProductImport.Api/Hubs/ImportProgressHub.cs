using Microsoft.AspNetCore.SignalR;

namespace ProductImport.Api.Hubs;

public class ImportProgressHub : Hub
{
    public async Task JoinGroup(string jobId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, jobId);
    }

    public async Task LeaveGroup(string jobId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, jobId);
    }
}
