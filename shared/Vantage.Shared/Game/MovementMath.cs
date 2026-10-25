using System.Numerics;

namespace Vantage.Shared.Game;

public static class MovementMath
{
    public const float MoveSpeed = 240f;

    public static Vector2 VelocityFromMove(Vector2 move)
    {
        if (move.LengthSquared() > 1f) move = Vector2.Normalize(move);
        return move * MoveSpeed;
    }
}
