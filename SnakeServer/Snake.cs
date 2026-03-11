using SnakeShared;

namespace SnakeServer;
using System.Numerics;
public class Snake
{
    private Vector2 _snakeHead;
    private readonly Queue<Vector2> _snakeTail = new();
    private Direction _movementDirection;

    public int Score => _snakeTail.Count;
    public int X => (int)_snakeHead.X;
    public int Y => (int)_snakeHead.Y;
    public Vector2 Position => _snakeHead;
    public Direction Direction => _movementDirection;

    public Snake(Vector2 gridDimension, List<Snake> snakes)
    {
        var spawnPos = PickRandomSpawnLocation(gridDimension, snakes);
        _snakeHead = new Vector2(spawnPos.X, spawnPos.Y);
        _movementDirection = Direction.Down;
    }

    public void ApplyMovementDirection(Direction direction)
    {
        if (direction != Direction.Invalid && !IsOppositeDirection(direction))
            _movementDirection = direction;
        GrowTail();
        AdvanceSnakeHead();
        MaintainTail();
    }

    private bool IsOppositeDirection(Direction newDirection)
    {
        if (_snakeTail.Count == 0) return false;

        return (newDirection == Direction.Up && _movementDirection == Direction.Down) ||
               (newDirection == Direction.Down && _movementDirection == Direction.Up) ||
               (newDirection == Direction.Left && _movementDirection == Direction.Right) ||
               (newDirection == Direction.Right && _movementDirection == Direction.Left);
    }

    private void AdvanceSnakeHead()
    {
        _snakeHead += _movementDirection switch
        {
            Direction.Left  => new Vector2(-1, 0),
            Direction.Right => new Vector2(1, 0),
            Direction.Up    => new Vector2(0, -1),
            Direction.Down  => new Vector2(0, 1),
            _ => Vector2.Zero
        };
    }
    public bool HeadExistsAtCoordinate(Vector2 coord)
    {
        return coord == _snakeHead;
    }

    public bool TailIntersectsWithCoordinate(Vector2 vector2)
    {
        return _snakeTail.Contains(vector2);
    }
    
    public void Respawn()
    {
        _snakeTail.Clear();
        _snakeHead = new Vector2(10, 2);
        _movementDirection = Direction.Down;
    }

    private void MaintainTail()
    {
        if (_snakeTail.Count > 0)
            _snakeTail.Dequeue();
    }

    public void GrowTail()
    {
        _snakeTail.Enqueue(new Vector2(_snakeHead.X, _snakeHead.Y));
    }

    public bool ShouldDie(Vector2 gridDimension)
    {
        return TailIntersectsWithCoordinate(_snakeHead) || CheckWallCollision(gridDimension);
    }
    
    private bool CheckWallCollision(Vector2 gridDimension)
    {
        return X <= 0 ||
               X >= gridDimension.X - 1 ||
               Y <= 0 ||
               Y >= gridDimension.Y - 1;
    }
    
    public void InitialiseTail(int length, Vector2 direction)
    {
        _snakeTail.Clear();
        for (var i = 1; i <= length; i++)
        {
            _snakeTail.Enqueue(_snakeHead - (direction * i));
        }
    }

    public List<Vector2> GetBody()
    {
        var body = new List<Vector2> { _snakeHead };
        foreach (var segment in _snakeTail)
            body.Add(segment);
        return body;
    }
    
    public static Vector2 PickRandomSpawnLocation(Vector2 gridDimension, List<Snake> snakes)
    {
        var rand = new Random();
        Vector2 newPos;
        bool isOccupied;

        do
        {
            var x = rand.Next(1, (int)gridDimension.X - 1);
            var y = rand.Next(1, (int)gridDimension.Y - 1);
            newPos = new Vector2(x, y);

            isOccupied = snakes.Any(s => 
                s.HeadExistsAtCoordinate(newPos) || 
                s.TailIntersectsWithCoordinate(newPos));

        } while (isOccupied);

        return newPos;
    }
}