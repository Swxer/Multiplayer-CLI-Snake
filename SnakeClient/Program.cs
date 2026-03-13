using System.Numerics;
using Microsoft.AspNetCore.SignalR.Client;
using SnakeShared;

public class Program
{
    private const int Width = 50;
    private const int Height = 25;
    public static async Task Main()
    {
        Console.Write("Enter your name: ");
        var playerName = Console.ReadLine() ?? "Player";
        
        var hubConnection = new HubConnectionBuilder()
            .WithUrl("http://localhost:5000/gameHub")
            .Build();
    
        hubConnection.On<GameState>("GameState", (state) =>
        {
            var clientConnectionId = hubConnection.ConnectionId;
            if (clientConnectionId != null) RenderGame(state, clientConnectionId);
        });
    
        await hubConnection.StartAsync();
        await hubConnection.InvokeAsync("SetName", playerName);
        
        
        _ = Task.Run(async () => {
            while (true)
            {
                await Task.Delay(100); 
            }
        });
        
        while (true)
        {
            var keyInfo = Console.ReadKey(intercept: true);
        
            var direction = keyInfo.Key switch
            {
                ConsoleKey.LeftArrow or ConsoleKey.A => Direction.Left,
                ConsoleKey.RightArrow or ConsoleKey.D => Direction.Right,
                ConsoleKey.UpArrow or ConsoleKey.W => Direction.Up,
                ConsoleKey.DownArrow or ConsoleKey.S => Direction.Down,
                _ => Direction.Invalid
            };
        
            if (direction != Direction.Invalid)
            {
                await hubConnection.InvokeAsync("Move", direction);
            }
        }
    }
    
    private static bool IsSnakeAtPosition(Position position, List<SnakeState> snakes)
    {
        return snakes.Any(snake => snake.Body.Any(segment => segment == position));
    }

    private static void RenderGame(GameState state, string clientConnectionId)
    {
        var frame = new System.Text.StringBuilder();
        var snakes = state.Snakes;

        const string cyanText = "\e[36m■\e[0m";
        const string whiteText = "\e[37m■\e[0m";
    
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var currentPos = new Position(x, y);

                foreach (var snake in snakes)
                {
                    if (snake.Body.Any(segment => segment == currentPos))
                    {
                        var isMySnake = (snake.ConnectionId == clientConnectionId);
                        frame.Append(isMySnake ? cyanText : whiteText);
                        break; 
                    }
                }
            
                if (!snakes.Any(s => s.Body.Any(segment => segment == currentPos)))
                {
                    if (currentPos == state.ApplePosition)
                        frame.Append('A');
                    else if (x == 0 || y == 0 || x == Width - 1 || y == Height - 1)
                        frame.Append('#');
                    else
                        frame.Append(' ');
                }
            }
            frame.AppendLine();
        }
        
        Console.SetCursorPosition(0, 0);
        Console.Write(frame.ToString());
        
        RenderScoreboard(state.Snakes);
    }
    
    private static void RenderScoreboard(List<SnakeState> snakes)
    {
        const int scoreboardStart = Height + 1;
        Console.SetCursorPosition(0, scoreboardStart);
        
        for (var i = 0; i < snakes.Count + 2; i++)
        {
            Console.WriteLine(new string(' ', 60));
        }
        
        Console.SetCursorPosition(0, scoreboardStart);
    
        Console.WriteLine("=== Scoreboard ===");
        foreach (var snake in snakes)
        {
            Console.WriteLine($"{snake.Name}: {snake.Score}");
        }
    }
}