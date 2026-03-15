namespace SnakeServer;
using System.Numerics;

public class Apple
{
    private static readonly Random Rand = new();
    private static Vector2 _apple = new(0, 0);

    public Apple(Vector2 gridDimensions, List<Snake> snakes)
    {
        PickRandomAppleLocation(gridDimensions, snakes);
    }
    public Vector2 Position => _apple;

    public static void PickRandomAppleLocation(Vector2 gridDimension, List<Snake> snakes)
    {
        bool isOccupied;
        Vector2 newPos;

        do
        {
            var x = Rand.Next(1, (int)gridDimension.X - 1);
            var y = Rand.Next(1, (int)gridDimension.Y - 1);
            newPos = new Vector2(x, y);
            
            isOccupied = snakes.Any(s => 
                s.HeadExistsAtCoordinate(newPos) || 
                s.TailIntersectsWithCoordinate(newPos));

        } while (isOccupied);
        
        _apple = newPos;
    }
}