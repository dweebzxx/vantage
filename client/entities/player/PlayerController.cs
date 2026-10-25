using Godot;
using Vantage.Shared.Game;

/// <summary>
/// Movement sandbox controller. Connected movement is displayed from server snapshots.
/// </summary>
[GlobalClass]
public partial class PlayerController : CharacterBody2D
{
    [Export]
    public float MoveSpeed { get; set; } = MovementMath.MoveSpeed;

    [Export]
    public NodePath DevDebugAimPivotPath { get; set; } = new("DevDebugAimPivot");

    public Vector2 AimDirection { get; private set; } = Vector2.Right;

    public float AimAngleRadians { get; private set; }

    public bool ViewOnly { get; set; }

    public void ApplySnapshot(Vector2 position, float aimAngle)
    {
        GlobalPosition = position;
        if (_devDebugAimPivot is not null) _devDebugAimPivot.Rotation = aimAngle;
    }

    private Node2D? _devDebugAimPivot;

    public override void _Ready()
    {
        _devDebugAimPivot = GetNodeOrNull<Node2D>(DevDebugAimPivotPath);
        UpdateLocalMouseAim();
    }

    public override void _Process(double delta)
    {
        UpdateLocalMouseAim();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (ViewOnly)
        {
            Velocity = Vector2.Zero;
            return;
        }
        Vector2 inputDirection = Input.GetVector("move_left", "move_right", "move_up", "move_down");
        var velocity = MovementMath.VelocityFromMove(new(inputDirection.X, inputDirection.Y));
        // Preserve the existing exported offline speed override.
        Velocity = new Vector2(velocity.X, velocity.Y) * (MoveSpeed / MovementMath.MoveSpeed);
        MoveAndSlide();
    }

    private void UpdateLocalMouseAim()
    {
        Vector2 playerToMouse = GetGlobalMousePosition() - GlobalPosition;

        if (playerToMouse.LengthSquared() <= Mathf.Epsilon)
        {
            return;
        }

        AimDirection = playerToMouse.Normalized();
        AimAngleRadians = AimDirection.Angle();

        if (!ViewOnly && _devDebugAimPivot is not null)
        {
            _devDebugAimPivot.Rotation = AimAngleRadians;
        }
    }
}
