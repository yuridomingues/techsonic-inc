using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace TicketPrime.Server.Hubs;

[Authorize]
public class SeatHub : Hub
{
    public async Task JoinEventGroup(int eventId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"event-{eventId}");
        await Clients.Group($"event-{eventId}").SendAsync("UserJoined", Context.UserIdentifier);
    }

    public async Task LeaveEventGroup(int eventId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"event-{eventId}");
    }

    public async Task RequestSeatLock(int eventId, int seatId)
    {
        // Validate seat availability and lock for current user
        var userId = Context.UserIdentifier;
        // In a real implementation, you would check database and acquire lock
        // For now, broadcast that seat is locked
        await Clients.Group($"event-{eventId}").SendAsync("SeatLocked", seatId, userId);
    }

    public async Task ReleaseSeatLock(int eventId, int seatId)
    {
        await Clients.Group($"event-{eventId}").SendAsync("SeatReleased", seatId);
    }

    public async Task JoinQueue(int eventId)
    {
        var userId = Context.UserIdentifier;
        // Logic to add user to queue and notify position
        var position = 1; // placeholder
        await Clients.Caller.SendAsync("QueuePosition", position);
        await Clients.Group($"event-{eventId}").SendAsync("QueueUpdated", userId, position);
    }

    public async Task LeaveQueue(int eventId)
    {
        var userId = Context.UserIdentifier;
        await Clients.Group($"event-{eventId}").SendAsync("QueueLeft", userId);
    }

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // Release any locks held by this connection
        await base.OnDisconnectedAsync(exception);
    }
}