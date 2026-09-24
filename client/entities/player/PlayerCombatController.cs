using Godot;
using Vantage.Shared.Game;

/// <summary>
/// Dev-only local combat probe for the movement sandbox.
/// Real gameplay systems, replication, and protocol are intentionally out of scope.
/// </summary>
[GlobalClass]
public partial class PlayerCombatController : Node
{
    [Export]
    public float DevDebugShotVisibleSeconds { get; set; } = 0.08f;

    [Export]
    public NodePath DevDebugShotLinePath { get; set; } = new("DevDebugShotLine");

    [Export]
    public PackedScene? DevProjectileScene { get; set; }

    [Export]
    public float DevMuzzleOffsetUnits { get; set; } = 28.0f;

    [Export]
    public PackedScene? DevGrenadeScene { get; set; }

    private readonly GrenadeDef _devGrenade = GrenadeDef.DevPlaceholder;

    private PlayerController? _player;
    private Line2D? _devDebugShotLine;
    private WeaponDef _activeWeapon = WeaponDef.DevHitscan;
    private double _cooldownRemaining;
    private double _shotVisibleRemaining;
    private double _grenadeCooldownRemaining;

    /// <summary>Identifier of the currently active weapon, for HUD display.</summary>
    public WeaponId ActiveWeaponId => _activeWeapon.Id;

    /// <summary>Seconds remaining before the active weapon can fire again.</summary>
    public double CooldownRemaining => _cooldownRemaining;

    /// <summary>Total fire-interval seconds for the active weapon, for HUD normalization.</summary>
    public double CooldownDuration => _activeWeapon.FireIntervalSeconds;

    /// <summary>Seconds remaining before another grenade can be thrown.</summary>
    public double GrenadeCooldownRemaining => _grenadeCooldownRemaining;

    /// <summary>Total grenade-throw cooldown seconds, for HUD normalization.</summary>
    public double GrenadeCooldownDuration => _devGrenade.FuseSeconds;

    public override void _Ready()
    {
        _player = GetParentOrNull<PlayerController>();
        _devDebugShotLine = GetNodeOrNull<Line2D>(DevDebugShotLinePath);

        if (_devDebugShotLine is not null)
        {
            _devDebugShotLine.TopLevel = true;
            _devDebugShotLine.Visible = false;
        }
    }

    public override void _Process(double delta)
    {
        if (_cooldownRemaining > 0.0)
        {
            _cooldownRemaining = Mathf.Max(0.0, _cooldownRemaining - delta);
        }

        if (_shotVisibleRemaining > 0.0)
        {
            _shotVisibleRemaining = Mathf.Max(0.0, _shotVisibleRemaining - delta);

            if (_shotVisibleRemaining <= 0.0 && _devDebugShotLine is not null)
            {
                _devDebugShotLine.Visible = false;
            }
        }

        if (_grenadeCooldownRemaining > 0.0)
        {
            _grenadeCooldownRemaining = Mathf.Max(0.0, _grenadeCooldownRemaining - delta);
        }

        if (Input.IsActionJustPressed("weapon_slot_1"))
        {
            SwitchActiveWeapon(WeaponDef.DevHitscan);
        }
        else if (Input.IsActionJustPressed("weapon_slot_2"))
        {
            SwitchActiveWeapon(WeaponDef.DevProjectile);
        }

        if (Input.IsActionJustPressed("primary_fire") && _cooldownRemaining <= 0.0)
        {
            FirePrimary();
        }

        if (Input.IsActionJustPressed("grenade_throw") && _grenadeCooldownRemaining <= 0.0)
        {
            FireLocalDebugGrenade();
        }
    }

    private void SwitchActiveWeapon(WeaponDef weapon)
    {
        if (_activeWeapon.Id == weapon.Id)
        {
            return;
        }

        _activeWeapon = weapon;
        GD.Print($"[DevCombat] Active weapon switched to {_activeWeapon.Id}");
    }

    private void FirePrimary()
    {
        switch (_activeWeapon.Kind)
        {
            case WeaponKind.Hitscan:
                FireLocalDebugShot();
                break;
            case WeaponKind.Projectile:
                FireLocalDebugProjectile();
                break;
        }

        _cooldownRemaining = _activeWeapon.FireIntervalSeconds;
    }

    private void FireLocalDebugShot()
    {
        if (_player is null)
        {
            return;
        }

        Vector2 from = _player.GlobalPosition;
        Vector2 to = from + (_player.AimDirection * _activeWeapon.RangeUnits);

        PhysicsRayQueryParameters2D query = PhysicsRayQueryParameters2D.Create(from, to);
        query.Exclude = new Godot.Collections.Array<Rid> { _player.GetRid() };
        query.CollideWithAreas = true;
        query.CollideWithBodies = true;

        Godot.Collections.Dictionary result = _player.GetWorld2D().DirectSpaceState.IntersectRay(query);
        Vector2 shotEnd = to;

        if (result.TryGetValue("position", out Variant positionVariant) && positionVariant.VariantType == Variant.Type.Vector2)
        {
            shotEnd = positionVariant.AsVector2();
        }

        if (result.TryGetValue("collider", out Variant colliderVariant) && colliderVariant.AsGodotObject() is Node colliderNode)
        {
            int damage = DamageMath.HitscanDamage(_activeWeapon, from.DistanceTo(shotEnd));
            ApplyDevTargetDebugDamage(colliderNode, damage);
        }

        ShowDebugShot(from, shotEnd);
    }

    private void FireLocalDebugProjectile()
    {
        if (_player is null || DevProjectileScene is null)
        {
            return;
        }

        if (DevProjectileScene.Instantiate() is not DevProjectileView projectile)
        {
            return;
        }

        Vector2 origin = _player.GlobalPosition + (_player.AimDirection * DevMuzzleOffsetUnits);

        Node spawnParent = _player.GetParentOrNull<Node>() ?? _player;
        spawnParent.AddChild(projectile);
        projectile.Configure(_activeWeapon, origin, _player.AimDirection, _player);
    }

    private void FireLocalDebugGrenade()
    {
        if (_player is null || DevGrenadeScene is null)
        {
            return;
        }

        if (DevGrenadeScene.Instantiate() is not DevGrenadeView grenade)
        {
            return;
        }

        Vector2 origin = _player.GlobalPosition + (_player.AimDirection * DevMuzzleOffsetUnits);

        Node spawnParent = _player.GetParentOrNull<Node>() ?? _player;
        spawnParent.AddChild(grenade);
        grenade.Configure(_devGrenade, origin, _player.AimDirection, _player);

        _grenadeCooldownRemaining = _devGrenade.FuseSeconds;
    }

    private static void ApplyDevTargetDebugDamage(Node colliderNode, int damage)
    {
        Node? current = colliderNode;

        while (current is not null)
        {
            if (TryApplyDevTargetDebugDamage(current, damage))
            {
                return;
            }

            current = current.GetParent();
        }

        if (colliderNode.Owner is Node ownerNode)
        {
            TryApplyDevTargetDebugDamage(ownerNode, damage);
        }
    }

    private static bool TryApplyDevTargetDebugDamage(Node node, int damage)
    {
        if (node is DevTargetDummy devTargetDummy)
        {
            devTargetDummy.ApplyDebugDamage(damage);
            return true;
        }

        if (node.HasMethod("ApplyDebugDamage"))
        {
            node.Call("ApplyDebugDamage", damage);
            return true;
        }

        if (node.HasMethod("RegisterDevHit"))
        {
            node.Call("RegisterDevHit", damage);
            return true;
        }

        return false;
    }

    private void ShowDebugShot(Vector2 from, Vector2 to)
    {
        if (_devDebugShotLine is null)
        {
            return;
        }

        _devDebugShotLine.GlobalPosition = Vector2.Zero;
        _devDebugShotLine.Points = new[] { from, to };
        _devDebugShotLine.Visible = true;
        _shotVisibleRemaining = DevDebugShotVisibleSeconds;
    }
}
