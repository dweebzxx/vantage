using Godot;
using Vantage.Shared.Game;
using Vantage.Shared.Net;
using Vantage.Shared.Protocol;
namespace Vantage.Server;

public sealed class WeaponSystem
{
    private sealed class Projectile(uint id, ServerPlayer owner, Vector2 position, Vector2 direction)
    {
        public uint Id { get; } = id;
        public uint Owner { get; } = owner.EntityId;
        public Faction Faction { get; } = owner.Faction;
        public Vector2 Position { get; set; } = position;
        public Vector2 Direction { get; } = direction;
        public float Distance { get; set; }
    }
    private readonly List<Projectile> _projectiles = new();
    public void Clear() => _projectiles.Clear();
    public ProjectileState[] Capture() => _projectiles.Select(p => new ProjectileState(p.Id, new(p.Position.X, p.Position.Y))).ToArray();

    public static void SelectAndAdvanceCooldowns(ServerPlayer player)
    {
        var select = (player.Input.LatestAccepted?.Buttons ?? InputButtons.None) & (InputButtons.SelectSlot1 | InputButtons.SelectSlot2);
        // Missing or contradictory selection leaves the previous choice intact.
        if (select == InputButtons.SelectSlot1) player.Weapon = WeaponDef.DevHitscan;
        else if (select == InputButtons.SelectSlot2) player.Weapon = WeaponDef.DevProjectile;
        if (player.FireRemaining > 0) player.FireRemaining--;
        if (player.GrenadeRemaining > 0) player.GrenadeRemaining--;
    }

    public void Fire(uint tick, ServerPlayer player, float aim, PhysicsDirectSpaceState2D space,
        IReadOnlyCollection<ServerPlayer> players, Func<uint> allocate, List<PendingDamage> damage)
    {
        if (player.FireRemaining > 0) return;
        var weapon = player.Weapon;
        player.FireRemaining = CombatRules.CooldownTicks(weapon.FireIntervalSeconds);
        GD.Print($"[Combat] shot tick={tick}; shooter={player.EntityId}; weapon={weapon.Id}; aim={aim}");
        Vector2 direction = Vector2.FromAngle(aim);
        Vector2 origin = player.Body.GlobalPosition;
        if (weapon.Kind == WeaponKind.Projectile)
        {
            uint id = allocate();
            _projectiles.Add(new(id, player, origin + direction * CombatRules.MuzzleOffset, direction));
            GD.Print($"[Combat] projectile-created tick={tick}; entity={id}; owner={player.EntityId}");
            return;
        }
        using var query = PhysicsRayQueryParameters2D.Create(origin, origin + direction * weapon.RangeUnits, 1);
        query.Exclude = new Godot.Collections.Array<Rid> { player.Body.GetRid() };
        query.CollideWithAreas = false;
        var hit = space.IntersectRay(query);
        if (hit.Count == 0) return;
        var target = players.FirstOrDefault(p => p.Body == hit["collider"].AsGodotObject());
        if (target is null)
        {
            GD.Print($"[Combat] wall-block tick={tick}; shooter={player.EntityId}; weapon={weapon.Id}");
            return;
        }
        float distance = origin.DistanceTo(hit["position"].AsVector2());
        damage.Add(new(player.EntityId, player.Faction, target, DamageMath.HitscanDamage(weapon, distance), distance, "hitscan"));
    }

    public void StepProjectiles(uint tick, PhysicsDirectSpaceState2D space, IReadOnlyCollection<ServerPlayer> players, List<PendingDamage> damage)
    {
        WeaponDef weapon = WeaponDef.DevProjectile;
        foreach (var projectile in _projectiles.ToArray())
        {
            float length = Math.Min(weapon.ProjectileSpeedUnitsPerSecond / TickMath.SimulationRateHz, weapon.RangeUnits - projectile.Distance);
            Vector2 step = projectile.Direction * length;
            // A capsule is the union of radius-6 circles along the entire step,
            // including both endpoints. Unlike endpoint overlaps it cannot tunnel.
            using var shape = new CapsuleShape2D { Radius = CombatRules.ProjectileRadius, Height = length + CombatRules.ProjectileRadius * 2 };
            using var query = new PhysicsShapeQueryParameters2D
            {
                Shape = shape,
                Transform = new Transform2D(projectile.Direction.Angle() + Mathf.Pi / 2, projectile.Position + step / 2),
                CollisionMask = 1,
                CollideWithBodies = true,
                CollideWithAreas = false,
            };
            var owner = players.FirstOrDefault(p => p.EntityId == projectile.Owner);
            // Explicit owner-body exclusion; the damage gate also rejects self damage.
            if (owner is not null) query.Exclude = new Godot.Collections.Array<Rid> { owner.Body.GetRid() };
            var hits = space.IntersectShape(query, 64);
            projectile.Distance += length;
            projectile.Position += step;
            bool wall = hits.Any(h => !players.Any(p => p.Body == h["collider"].AsGodotObject()));
            var target = players.Where(p => p.EntityId != projectile.Owner && hits.Any(h => h["collider"].AsGodotObject() == p.Body))
                .OrderBy(p => (p.Body.GlobalPosition - (projectile.Position - step)).Dot(projectile.Direction)).FirstOrDefault();
            if (wall || target is not null || projectile.Distance >= weapon.RangeUnits)
            {
                if (!wall && target is not null)
                    damage.Add(new(projectile.Owner, projectile.Faction, target, DamageMath.DamageByRange(weapon.Damage, projectile.Distance, weapon.RangeUnits), projectile.Distance, "projectile"));
                GD.Print($"[Combat] projectile-removed tick={tick}; entity={projectile.Id}; owner={projectile.Owner}; distance={projectile.Distance}; reason={(wall ? "wall" : target is not null ? "player" : "range")}");
                _projectiles.Remove(projectile);
            }
        }
    }
}
