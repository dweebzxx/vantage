using Godot;

/// <summary>
/// Local-only movement controller for the M2 movement sandbox.
/// Network authority, prediction, replication, and protocol concerns are intentionally out of scope.
/// </summary>
[GlobalClass]
public partial class PlayerController : CharacterBody2D
{
    [Export]
    public float MoveSpeed { get; set; } = 240.0f;

    public override void _PhysicsProcess(double delta)
    {
        Vector2 inputDirection = Input.GetVector("move_left", "move_right", "move_up", "move_down");

        if (inputDirection.LengthSquared() > 1.0f)
        {
            inputDirection = inputDirection.Normalized();
        }

        Velocity = inputDirection * MoveSpeed;
        MoveAndSlide();
    }
}
