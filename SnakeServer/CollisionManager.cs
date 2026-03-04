namespace SnakeServer;
using System.Numerics;

public static class CollisionManager
{
    public static bool CheckAppleCollision(Snake snake, Apple apple)
    {
        return snake.Position == apple.Position;
    }

    public static void HandleSnakesCollision(List<Snake> snakes, Vector2 grid)
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