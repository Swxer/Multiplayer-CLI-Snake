using System.Numerics;

namespace SnakeShared;

public record GameState(
    List<SnakeState> Snakes,
    Vector2 ApplePosition
);