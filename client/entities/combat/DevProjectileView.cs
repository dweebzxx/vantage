using Godot;
using Vantage.Shared.Game;

/// <summary>
/// Local-only dev placeholder for a travelling projectile, fired from PlayerCombatController.
/// Real gameplay systems, replication, and protocol are intentionally out of scope.
/// </summary>
[GlobalClass]
public partial class DevProjectileView : Area2D
{
    [Export]
    public NodePath DevPlaceholderBodyPath { get; set; } = new("DevPlaceholderBody");

    private WeaponDef _weaponDef;
    private Vector2 _velocity;
    private double _lifetimeRemainingSeconds;
    private float _distanceTraveledUnits;
    private bool _resolved;
    private Node2D? _ownerToIgnore;

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
        AreaEntered += OnAreaEntered;
    }

    public void Configure(WeaponDef weaponDef, Vector2 originPosition, Vector2 direction, Node2D ownerToIgnore)
    {
        _weaponDef = weaponDef;
        GlobalPosition = originPosition;

        Vector2 safeDirection = direction.LengthSquared() > Mathf.Epsilon ? direction.Normalized() : Vector2.Right;
        _velocity = safeDirection * weaponDef.ProjectileSpeedUnitsPerSecond;

        _lifetimeRemainingSeconds = weaponDef.ProjectileSpeedUnitsPerSecond > 0.0f
            ? weaponDef.RangeUnits / weaponDef.ProjectileSpeedUnitsPerSecond
            : 0.0;

        _ownerToIgnore = ownerToIgnore;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_resolved)
        {
            return;
        }

        Vector2 step = _velocity * (float)delta;
        GlobalPosition += step;
        _distanceTraveledUnits += step.Length();

        _lifetimeRemainingSeconds -= delta;
        if (_lifetimeRemainingSeconds <= 0.0)
        {
            QueueFree();
        }
    }

    private void OnBodyEntered(Node2D body)
    {
        if (body == _ownerToIgnore)
        {
            return;
        }

        ResolveImpact();
    }

    private void OnAreaEntered(Area2D area)
    {
        if (area is DevTargetDummy devTargetDummy)
        {
            int damage = DamageMath.DamageByRange(_weaponDef.Damage, _distanceTraveledUnits, _weaponDef.RangeUnits);
            devTargetDummy.ApplyDebugDamage(damage);
        }

        ResolveImpact();
    }

    private void ResolveImpact()
    {
        if (_resolved)
        {
            return;
        }

        _resolved = true;
        QueueFree();
    }
}
