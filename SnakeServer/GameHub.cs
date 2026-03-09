using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace SnakeServer;

public class GameHub : Hub
{
    public static readonly ConcurrentDictionary<string, Snake> Players = new();

    public override async Task OnConnectedAsync()
    {
        await Clients.All.SendAsync("Snake Oil", $"Player {Context.ConnectionId} connected");
  
    }

    public async Task Move(string direction)
    {
        await Clients.All.SendAsync("Snake Move", $"Player {Context.ConnectionId} moving {direction}");
    }
}