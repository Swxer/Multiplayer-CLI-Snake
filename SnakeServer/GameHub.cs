using Microsoft.AspNetCore.SignalR;
using SnakeShared;


namespace SnakeServer;

public class GameHub : Hub
{
    private readonly GameEngine _gameEngine;
    
    public GameHub(GameEngine gameEngine)
    {
        _gameEngine = gameEngine;
    }
    
    public override async Task OnConnectedAsync()
    {
        _gameEngine.AddPlayer(Context.ConnectionId);
        await Clients.All.SendAsync("Snake Player", $"Player {Context.ConnectionId} connected");
    }
    
    public Task SetName(string name)
    {
        _gameEngine.SetPlayerName(Context.ConnectionId, name);
        return Task.CompletedTask;
    }
    
    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _gameEngine.RemovePlayer(Context.ConnectionId);
        return Task.CompletedTask;
    }

    public Task Move(Direction direction)
    {
        _gameEngine.QueueInput(Context.ConnectionId, direction);
        return Task.CompletedTask;
    }
}