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

    [Export]
    public NodePath DevDebugAimPivotPath { get; set; } = new("DevDebugAimPivot");

    public Vector2 AimDirection { get; private set; } = Vector2.Right;

    public float AimAngleRadians { get; private set; }

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
        Vector2 inputDirection = Input.GetVector("move_left", "move_right", "move_up", "move_down");

        if (inputDirection.LengthSquared() > 1.0f)
        {
            inputDirection = inputDirection.Normalized();
        }

        Velocity = inputDirection * MoveSpeed;
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

        if (_devDebugAimPivot is not null)
        {
            _devDebugAimPivot.Rotation = AimAngleRadians;
        }
    }
}
