using System.Numerics;
using Microsoft.AspNetCore.SignalR.Client;
using SnakeShared;

public class Program
{
    const int Width = 50;
    const int Height = 20;
    public static async Task Main()
    {
        Console.Write("Enter your name: ");
        var playerName = Console.ReadLine() ?? "Player";
        
        var hubConnection = new HubConnectionBuilder()
            .WithUrl("http://localhost:5000/gameHub")
            .Build();
    
        hubConnection.On<GameState>("GameState", (state) => {
            RenderGame(state); 
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

    private static void RenderGame(GameState state)
    {
        Console.SetCursorPosition(0, 0);
        Console.Write("\u001b[H");
            
        var snakes = state.Snakes;
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var currentPos = new Position(x, y);

                if (IsSnakeAtPosition(currentPos, snakes))
                    Console.Write("■");
                else if (currentPos == state.ApplePosition)
                    Console.Write("A");
                else if (x == 0 || y == 0 || x == Width - 1 || y == Height - 1)
                    Console.Write('#');
                else
                    Console.Write(' ');
            }
            Console.WriteLine();
        }
        
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