using Godot;
using Vantage.Shared.Game;

namespace Vantage.Server;

public static class MovementSystem
{
    public static void Step(CharacterBody2D body, PeerInputCounters input)
    {
        var move = input.LatestAccepted?.Move ?? System.Numerics.Vector2.Zero;
        var velocity = MovementMath.VelocityFromMove(move);
        body.Velocity = new Vector2(velocity.X, velocity.Y);
        body.MoveAndSlide();
    }
}
