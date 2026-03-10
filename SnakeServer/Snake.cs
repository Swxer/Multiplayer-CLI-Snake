using SnakeShared;

namespace SnakeServer;
using System.Numerics;
public class Snake(int startingX, int startingY)
{
    private Vector2 _snakeHead = new(startingX, startingY);
    private readonly Queue<Vector2> _snakeTail = new();
    private Direction _movementDirection = Direction.Down;

    public int Score => _snakeTail.Count;
    public int X => (int)_snakeHead.X;
    public int Y => (int)_snakeHead.Y;
    public Vector2 Position => _snakeHead;

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
}