using System.Numerics;

namespace SnakeShared;

public record SnakeState(
    string ConnectionId,
    List<Position> Body,
    Direction Direction,
    int Score
);