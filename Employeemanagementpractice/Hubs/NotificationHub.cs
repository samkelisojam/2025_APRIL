using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace Employeemanagementpractice.Hubs
{
    public class NotificationHub : Hub
    {
        // Thread-safe dictionary: connectionId -> { email, fullName, role, connectedAt }
        private static readonly ConcurrentDictionary<string, OnlineUser> ConnectedUsers = new();

        public async Task SendRefreshSignal(string area)
        {
            await Clients.Others.SendAsync("ReceiveRefresh", area);
        }

        public async Task NotifyDataChange(string entityType, string action)
        {
            await Clients.Others.SendAsync("DataChanged", entityType, action);
        }

        public async Task SendAnnouncement(string title, string message)
        {
            await Clients.All.SendAsync("NewAnnouncement", title, message);
        }

        public override async Task OnConnectedAsync()
        {
            var email = Context.User?.Identity?.Name ?? "Unknown";
            var user = new OnlineUser
            {
                ConnectionId = Context.ConnectionId,
                Email = email,
                ConnectedAt = DateTime.UtcNow
            };
            ConnectedUsers[Context.ConnectionId] = user;

            // Broadcast updated user list to all clients
            await Clients.All.SendAsync("OnlineUsersUpdated", GetOnlineUserList());
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            ConnectedUsers.TryRemove(Context.ConnectionId, out _);

            // Broadcast updated user list to all clients
            await Clients.All.SendAsync("OnlineUsersUpdated", GetOnlineUserList());
            await base.OnDisconnectedAsync(exception);
        }

        public async Task SetUserInfo(string fullName, string role)
        {
            if (ConnectedUsers.TryGetValue(Context.ConnectionId, out var user))
            {
                user.FullName = fullName;
                user.Role = role;
                await Clients.All.SendAsync("OnlineUsersUpdated", GetOnlineUserList());
            }
        }

        public Task<List<OnlineUserDto>> GetOnlineUsers()
        {
            return Task.FromResult(GetOnlineUserList());
        }

        private static List<OnlineUserDto> GetOnlineUserList()
        {
            return ConnectedUsers.Values
                .GroupBy(u => u.Email.ToLower())
                .Select(g => g.OrderByDescending(u => u.ConnectedAt).First())
                .Select(u => new OnlineUserDto
                {
                    Email = u.Email,
                    FullName = u.FullName ?? u.Email,
                    Role = u.Role ?? "",
                    ConnectedAt = u.ConnectedAt
                })
                .OrderBy(u => u.FullName)
                .ToList();
        }
    }

    public class OnlineUser
    {
        public string ConnectionId { get; set; } = "";
        public string Email { get; set; } = "";
        public string? FullName { get; set; }
        public string? Role { get; set; }
        public DateTime ConnectedAt { get; set; }
    }

    public class OnlineUserDto
    {
        public string Email { get; set; } = "";
        public string FullName { get; set; } = "";
        public string Role { get; set; } = "";
        public DateTime ConnectedAt { get; set; }
    }
}
