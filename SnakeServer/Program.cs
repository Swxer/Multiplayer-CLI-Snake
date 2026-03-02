namespace SnakeServer;
using System.Numerics;

public class Program
{
    private const int TargetFps = 16;
    private const int Width = 50;
    private const int Height = 20;
    private static readonly Vector2 GridDimensions = new(Width, Height);

    private static void Main(string[] args)
    {
        Console.CursorVisible = false;
        Console.Clear();
        
        List<Snake> snakes =
        [
            new Snake(10, 2),
            new Snake(20, 10)
        ];
        
        // dummy snake, create tail downwards
        snakes[1].InitialiseTail(5, new Vector2(0, -1));
        var player = snakes[0];
        Apple apple = new(GridDimensions, snakes);
        
        while (true)
        {
            Console.SetCursorPosition(0, 0);
            Console.WriteLine("Score: " + player.Score);

            var isEating = CheckAppleCollision(player, apple);
            if (isEating)
                Apple.PickRandomAppleLocation(GridDimensions, snakes);

            player.ApplyMovementDirection(GetMovementInput(), isEating);
            HandleSnakesCollision(snakes, GridDimensions);
            RenderGame(GridDimensions, snakes, apple);
            Thread.Sleep(1000 / TargetFps);
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
    private static bool CheckAppleCollision(Snake snake, Apple apple)
    {
        return snake.Position == apple.Position;
    }

    private static void HandleSnakesCollision(List<Snake> snakes, Vector2 grid)
    {
        HashSet<Snake> deadSnakes = [];

        foreach (var snake in snakes)
        {
            // hit wall or eat its own tail
            if (snake.ShouldDie(grid))
            {
                deadSnakes.Add(snake);
                continue;
            }
            
            // snake collide with other snake
            foreach (var other in snakes)
            {
                if (snake == other) continue;
                
                // head to head
                if (other.HeadExistsAtCoordinate(snake.Position))
                {
                    deadSnakes.Add(snake);
                    deadSnakes.Add(other);
                }
                // snake bite other's tail
                else if (other.TailIntersectsWithCoordinate(snake.Position))
                {
                    deadSnakes.Add(snake);
                }
            }
        }

        foreach (var victim in deadSnakes)
        {
            victim.Respawn();
        }
    }
}