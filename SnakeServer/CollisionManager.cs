namespace SnakeServer;
using System.Numerics;

public static class CollisionManager
{
    public static bool IsEatingApple(Snake snake, Apple apple)
    {
        return snake.Position == apple.Position;
    }

    public static void EatApple(List<Snake> snakes, Apple apple, Vector2 grid)
    {
        foreach (var snake in snakes)
            if (IsEatingApple(snake, apple))
            {
                Console.WriteLine($"Snake eating apple! Position: {snake.Position}, Apple: {apple.Position}");
                snake.GrowTail();
                Apple.PickRandomAppleLocation(grid, snakes);
            }
    }

    private static void RespawnDeadSnakes(HashSet<Snake> deadSnakes, Vector2 grid, List<Snake> snakes)
    {
        foreach (var victim in deadSnakes)
        {
            victim.Respawn(grid, snakes);
        }
    }

    private static void FindDeadSnakes(HashSet<Snake> deadSnakes, List<Snake> snakes, Vector2 grid)
    {
        foreach (var snake in snakes)
        {
            if (snake.ShouldDie(grid))
            {
                deadSnakes.Add(snake);
                continue;
            }
            
            foreach (var other in snakes)
            {
                if (snake == other) continue;
                
                if (other.HeadExistsAtCoordinate(snake.Position))
                {
                    deadSnakes.Add(snake);
                    deadSnakes.Add(other);
                }
                else if (other.TailIntersectsWithCoordinate(snake.Position))
                {
                    deadSnakes.Add(snake);
                }
            }
        }
    }

    public static void HandleCollision(List<Snake> snakes, Vector2 grid)
    {
        HashSet<Snake> deadSnakes = [];
        FindDeadSnakes(deadSnakes, snakes, grid);
        RespawnDeadSnakes(deadSnakes, grid, snakes);
    }
}