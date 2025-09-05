using Microsoft.AspNetCore.SignalR;

namespace HomeSpeaker.Server2.Hubs;

public class AnchorHub : Hub
{
    public async Task JoinAnchorGroup()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "AnchorUpdates");
    }

    public async Task LeaveAnchorGroup()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "AnchorUpdates");
    }
}