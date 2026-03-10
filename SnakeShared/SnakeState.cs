using System.Numerics;

namespace SnakeShared;

public record SnakeState(
    string ConnectionId,
    List<Vector2> Body,
    Direction Direction,
    int Score
);