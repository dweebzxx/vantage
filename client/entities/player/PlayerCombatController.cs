using Godot;
using Vantage.Shared.Game;

/// <summary>
/// Dev-only local combat probe for the movement sandbox.
/// Real gameplay systems, replication, and protocol are intentionally out of scope.
/// </summary>
[GlobalClass]
public partial class PlayerCombatController : Node
{
    private static readonly WeaponDef DevHitscanWeapon = WeaponDef.DevHitscan;

    [Export]
    public float DevDebugShotVisibleSeconds { get; set; } = 0.08f;

    [Export]
    public NodePath DevDebugShotLinePath { get; set; } = new("DevDebugShotLine");

    private PlayerController? _player;
    private Line2D? _devDebugShotLine;
    private double _cooldownRemaining;
    private double _shotVisibleRemaining;

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

        if (Input.IsActionJustPressed("primary_fire") && _cooldownRemaining <= 0.0)
        {
            FireLocalDebugShot();
        }
    }

    private void FireLocalDebugShot()
    {
        if (_player is null)
        {
            return;
        }

        Vector2 from = _player.GlobalPosition;
        Vector2 to = from + (_player.AimDirection * DevHitscanWeapon.RangeUnits);

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
            int damage = DamageMath.HitscanDamage(DevHitscanWeapon, from.DistanceTo(shotEnd));
            ApplyDevTargetDebugDamage(colliderNode, damage);
        }

        ShowDebugShot(from, shotEnd);
        _cooldownRemaining = DevHitscanWeapon.FireIntervalSeconds;
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
