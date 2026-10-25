using System.Numerics;
using Vantage.Shared.Game;
using Xunit;

namespace Vantage.Shared.Tests;

public sealed class MovementMathTests
{
    [Fact]
    public void Zero_move_has_zero_velocity() => Assert.Equal(Vector2.Zero, MovementMath.VelocityFromMove(Vector2.Zero));

    [Fact]
    public void Axis_move_has_speed_240() => Assert.Equal(new Vector2(240, 0), MovementMath.VelocityFromMove(Vector2.UnitX));

    [Fact]
    public void Diagonal_move_is_capped_at_speed()
    {
        Vector2 velocity = MovementMath.VelocityFromMove(new(1, 1));
        Assert.InRange(MathF.Abs(velocity.Length() - 240f), 0, 0.0001f);
        Assert.InRange(MathF.Abs(velocity.X - 169.70563f), 0, 0.0001f);
        Assert.Equal(velocity.X, velocity.Y);
    }

    [Fact]
    public void Inside_unit_circle_preserves_analog_magnitude() =>
        Assert.Equal(new Vector2(-72, 96), MovementMath.VelocityFromMove(new(-0.3f, 0.4f)));
}
