using System.Numerics;

namespace SnakeShared;

public readonly record struct Position(int X, int Y)
{
    public static implicit operator Position(Vector2 v) => new((int)v.X, (int)v.Y);
    public static implicit operator Vector2(Position p) => new(p.X, p.Y);
}