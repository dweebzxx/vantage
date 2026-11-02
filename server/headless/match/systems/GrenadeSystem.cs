using Godot;
using Vantage.Shared.Game;
using Vantage.Shared.Net;
using Vantage.Shared.Protocol;
namespace Vantage.Server;

public sealed class GrenadeSystem
{
    private sealed class Grenade(uint id, ServerPlayer owner, Vector2 origin, Vector2 direction, uint tick)
    {
        public uint Id { get; } = id;
        public uint Owner { get; } = owner.EntityId;
        public Faction Faction { get; } = owner.Faction;
        public Vector2 Position { get; set; } = origin;
        public Vector2 Destination { get; } = origin + direction * CombatRules.GrenadeTravelDistance;
        public uint ThrownTick { get; } = tick;
    }
    private readonly List<Grenade> _grenades = new();
    public void Clear() => _grenades.Clear();
    public GrenadeState[] Capture() => _grenades.Select(g => new GrenadeState(g.Id, new(g.Position.X, g.Position.Y))).ToArray();

    public void Throw(uint tick, ServerPlayer player, float aim, Func<uint> allocate)
    {
        if (player.GrenadeRemaining > 0) return;
        player.GrenadeRemaining = CombatRules.CooldownTicks(GrenadeDef.DevPlaceholder.FuseSeconds);
        Vector2 direction = Vector2.FromAngle(aim);
        Vector2 origin = player.Body.GlobalPosition + direction * CombatRules.MuzzleOffset;
        var grenade = new Grenade(allocate(), player, origin, direction, tick);
        _grenades.Add(grenade);
        GD.Print($"[Combat] grenade-created tick={tick}; entity={grenade.Id}; owner={grenade.Owner}; origin={origin}; destination={grenade.Destination}; total-distance={CombatRules.MuzzleOffset + CombatRules.GrenadeTravelDistance}");
    }

    public void Step(uint tick, IReadOnlyCollection<ServerPlayer> players, List<PendingDamage> damage)
    {
        var def = GrenadeDef.DevPlaceholder;
        foreach (var grenade in _grenades.ToArray())
        {
            // No movement/time is charged on the creation tick. Detonate at throw+90.
            if (tick == grenade.ThrownTick) continue;
            grenade.Position = grenade.Position.MoveToward(grenade.Destination, CombatRules.GrenadeTravelSpeed / TickMath.SimulationRateHz);
            if (TickMath.Delta(tick, grenade.ThrownTick) < CombatRules.CooldownTicks(def.FuseSeconds)) continue;
            GD.Print($"[Combat] grenade-detonated tick={tick}; entity={grenade.Id}; owner={grenade.Owner}; thrown={grenade.ThrownTick}; elapsed={TickMath.Delta(tick, grenade.ThrownTick)}; position={grenade.Position}");
            // Centre distances match local DamageMath, independent of walls and body size.
            foreach (var target in players)
            {
                float distance = grenade.Position.DistanceTo(target.Body.GlobalPosition);
                bool eligible = target.EntityId != grenade.Owner && CombatRules.MayDamage(grenade.Faction, target.Faction);
                int amount = eligible ? DamageMath.RadiusDamage(def, distance) : 0;
                GD.Print($"[Combat] grenade-target tick={tick}; entity={grenade.Id}; target={target.EntityId}; distance={distance}; amount={amount}; eligible={eligible}");
                if (amount > 0) damage.Add(new(grenade.Owner, grenade.Faction, target, amount, distance, "grenade"));
            }
            _grenades.Remove(grenade);
        }
    }
}
