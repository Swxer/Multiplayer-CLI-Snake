namespace SnakeServer;
using System.Numerics;

public class Program
{
    private const int TargetFps = 10;
    private const int Width = 50;
    private const int Height = 20;
    private static readonly Vector2 GridDimensions = new(Width, Height);
    private static readonly List<Snake> Snakes = [];

    private static async Task Main(string[] args)
    {
        Console.CursorVisible = false;
        Console.Clear();
        Snakes.Add(new Snake(10, 2));
        Snakes.Add(new Snake(20, 10));
        
        // dummy snake, create tail downwards
        Snakes[1].InitialiseTail(5, new Vector2(0, -1));
        var player = Snakes[0];
        Apple apple = new(GridDimensions, Snakes);

        var intervalMs = 1000 / TargetFps;
        using PeriodicTimer timer = new(TimeSpan.FromMilliseconds(intervalMs));
        
        while (await timer.WaitForNextTickAsync())
        {
            Console.SetCursorPosition(0, 0);
            Console.WriteLine("Score: " + player.Score);

            var isEating = CollisionManager.CheckAppleCollision(player, apple);
            if (isEating)
                Apple.PickRandomAppleLocation(GridDimensions, Snakes);

            player.ApplyMovementDirection(GetMovementInput(), isEating);
            CollisionManager.HandleSnakesCollision(Snakes, GridDimensions);
            RenderGame(GridDimensions, Snakes, apple);
        }
    }

    private static Direction GetMovementInput()
    {
        if (!Console.KeyAvailable) return Direction.Invalid;
        var key = Console.ReadKey(true).Key;

        var movementDirection = key switch
        {
            ConsoleKey.LeftArrow or ConsoleKey.A => Direction.Left,
            ConsoleKey.RightArrow or ConsoleKey.D => Direction.Right,
            ConsoleKey.UpArrow or ConsoleKey.W => Direction.Up,
            ConsoleKey.DownArrow or ConsoleKey.S => Direction.Down,
            _ => Direction.Invalid
        };

        return movementDirection;
    }

    private static void RenderGame(Vector2 grid, List<Snake> snakes, Apple apple)
    {
        var width = (int)grid.X;
        var height = (int)grid.Y;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var currentPos = new Vector2(x, y);
                
                if (snakes.Any(s => s.HeadExistsAtCoordinate(currentPos)))
                    Console.Write('■');
                else if (snakes.Any(s => s.TailIntersectsWithCoordinate(currentPos)))
                    Console.Write('T');
                else if (Apple.AppleExistsAtCoordinate(currentPos))
                    Console.Write('A');
                else if (x == 0 || y == 0 || x == width - 1 || y == height - 1)
                    Console.Write('#');
                else
                    Console.Write(' ');
            }
            Console.WriteLine();
        }
    }
}