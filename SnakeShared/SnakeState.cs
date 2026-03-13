using System.Numerics;

namespace SnakeShared;

public record SnakeState(
    string ConnectionId,
    string Name,
    List<Position> Body,
    Direction Direction,
    int Score
);