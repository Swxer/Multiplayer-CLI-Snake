using System.Numerics;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using SnakeShared;

namespace SnakeServer;

public class GameEngine
{
    private const int TargetFps = 10;
    private const int Width = 50;
    private const int Height = 25;
    private static readonly Vector2 GridDimensions = new(Width, Height);

    private readonly ConcurrentDictionary<string, Snake> _snakes = new();
    private readonly ConcurrentDictionary<string, Direction> _pendingInputs = new();
    private readonly ConcurrentDictionary<string, string> _playerNames = new();
    private Apple? _apple;
    private PeriodicTimer? _timer;
    private IHubContext<GameHub>? _hubContext;

    public void SetHubContext(IHubContext<GameHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task StartAsync()
    {
        if (_hubContext == null)
            throw new InvalidOperationException("HubContext not set. Call SetHubContext first.");

        _apple = new Apple(GridDimensions, _snakes.Values.ToList());

        var intervalMs = 1000 / TargetFps;
        _timer = new PeriodicTimer(TimeSpan.FromMilliseconds(intervalMs));

        while (await _timer.WaitForNextTickAsync())
        {
            await Tick();
        }
    }

    public Task StopAsync()
    {
        _timer?.Dispose();
        return Task.CompletedTask;
    }

    public void AddPlayer(string connectionId)
    {
        var snake = new Snake(GridDimensions, _snakes.Values.ToList());
        _snakes.TryAdd(connectionId, snake);
    }

    public void RemovePlayer(string connectionId)
    {
        _snakes.TryRemove(connectionId, out _);
        _pendingInputs.TryRemove(connectionId, out _);
        _playerNames.TryRemove(connectionId, out _);
    }

    public void QueueInput(string connectionId, Direction direction)
    {
        if (direction != Direction.Invalid)
            _pendingInputs[connectionId] = direction;
    }
    
    public void SetPlayerName(string connectionId, string name)
    {
        _playerNames[connectionId] = name;
    }

    private async Task Tick()
    {
        foreach (var (connectionId, snake) in _snakes)
        {
            if (_pendingInputs.TryGetValue(connectionId, out var direction))
            {
                snake.ApplyMovementDirection(direction);
                _pendingInputs.TryRemove(connectionId, out _);
            }
            else
            {
                snake.ApplyMovementDirection(Direction.Invalid);
            }
        }

        if (_apple != null)
        {
            CollisionManager.HandleCollision(_snakes.Values.ToList(), GridDimensions);
            CollisionManager.EatApple(_snakes.Values.ToList(), _apple, GridDimensions);
        }

        await BroadcastState();
    }

    private async Task BroadcastState()
    {
        if (_hubContext == null) 
            return;

        var snakeStates = new List<SnakeState>();
    
        foreach (var (connectionId, snake) in _snakes)
        {
            snakeStates.Add(new SnakeState(
                connectionId,
                _playerNames.GetValueOrDefault(connectionId, "Player"),
                GetSnakeBody(snake),
                snake.Direction,
                snake.Score
            ));
        }

        Position applePosition = _apple?.Position ?? Vector2.Zero;
        var gameState = new GameState(snakeStates, applePosition);
        await _hubContext.Clients.All.SendAsync("GameState", gameState);
    }
    private static List<Position> GetSnakeBody(Snake snake)
    {
        return snake.GetBody().Select(p => new Position((int)p.X, (int)p.Y)).ToList();
    }
}