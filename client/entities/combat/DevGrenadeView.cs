using System.Collections.Generic;
using Godot;
using Vantage.Shared.Game;

/// <summary>
/// Local-only dev placeholder for a thrown grenade, fired from PlayerCombatController.
/// Real gameplay systems, replication, and protocol are intentionally out of scope.
/// </summary>
[GlobalClass]
public partial class DevGrenadeView : Node2D
{
    [Export]
    public float ThrowSpeedUnitsPerSecond { get; set; } = 420.0f;

    [Export]
    public float ThrowDistanceUnits { get; set; } = 220.0f;

    [Export]
    public uint DevTargetCollisionMask { get; set; } = 1;

    private GrenadeDef _grenadeDef;
    private Vector2 _travelTarget;
    private bool _traveling;
    private double _fuseRemainingSeconds;
    private bool _detonated;

    public void Configure(GrenadeDef grenadeDef, Vector2 originPosition, Vector2 direction, Node2D ownerToIgnore)
    {
        _ = ownerToIgnore;

        _grenadeDef = grenadeDef;
        GlobalPosition = originPosition;

        Vector2 safeDirection = direction.LengthSquared() > Mathf.Epsilon ? direction.Normalized() : Vector2.Right;
        _travelTarget = originPosition + (safeDirection * ThrowDistanceUnits);
        _traveling = true;
        _fuseRemainingSeconds = grenadeDef.FuseSeconds;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_detonated)
        {
            return;
        }

        if (_traveling)
        {
            Vector2 toTarget = _travelTarget - GlobalPosition;
            float step = ThrowSpeedUnitsPerSecond * (float)delta;

            if (toTarget.Length() <= step)
            {
                GlobalPosition = _travelTarget;
                _traveling = false;
            }
            else
            {
                GlobalPosition += toTarget.Normalized() * step;
            }
        }

        _fuseRemainingSeconds -= delta;

        if (_fuseRemainingSeconds <= 0.0)
        {
            Detonate();
        }
    }

    private void Detonate()
    {
        _detonated = true;
        ApplyRadiusDamage();
        QueueFree();
    }

    private void ApplyRadiusDamage()
    {
        CircleShape2D shape = new() { Radius = _grenadeDef.RadiusUnits };
        PhysicsShapeQueryParameters2D query = new()
        {
            Shape = shape,
            Transform = new Transform2D(0.0f, GlobalPosition),
            CollideWithAreas = true,
            CollideWithBodies = false,
            CollisionMask = DevTargetCollisionMask,
        };

        Godot.Collections.Array<Godot.Collections.Dictionary> results =
            GetWorld2D().DirectSpaceState.IntersectShape(query, 32);

        HashSet<ulong> handledInstanceIds = new();

        foreach (Godot.Collections.Dictionary result in results)
        {
            if (!result.TryGetValue("collider", out Variant colliderVariant))
            {
                continue;
            }

            if (colliderVariant.AsGodotObject() is not DevTargetDummy dummy)
            {
                continue;
            }

            if (!handledInstanceIds.Add(dummy.GetInstanceId()))
            {
                continue;
            }

            float distanceUnits = GlobalPosition.DistanceTo(dummy.GlobalPosition);
            int damage = DamageMath.RadiusDamage(_grenadeDef, distanceUnits);
            dummy.ApplyDebugDamage(damage);
        }
    }
}
