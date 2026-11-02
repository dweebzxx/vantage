using Godot;
using Vantage.Shared.Game;
namespace Vantage.Server;

public sealed class ServerPlayer(uint entityId, Faction team, CharacterBody2D body, PeerInputCounters input)
{
    public uint EntityId { get; } = entityId;
    public CharacterBody2D Body { get; } = body;
    public PeerInputCounters Input { get; } = input;
    // Mirrors this player's MatchState row; changed only by the join and the rebalance.
    public Faction Faction { get; set; } = team;
    public int Health { get; set; } = CombatRules.DevPlayerMaxHealth;
    public WeaponDef Weapon { get; set; } = WeaponDef.DevHitscan;
    public uint FireRemaining { get; set; }
    public uint GrenadeRemaining { get; set; }
}

public readonly record struct PendingDamage(uint Source, Faction Faction, ServerPlayer Target, int Amount, float Distance, string Weapon);
