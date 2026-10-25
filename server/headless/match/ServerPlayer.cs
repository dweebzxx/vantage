using Godot;
using Vantage.Shared.Game;
namespace Vantage.Server;

public sealed class ServerPlayer(uint entityId, int spawnIndex, CharacterBody2D body, PeerInputCounters input)
{
    public uint EntityId { get; } = entityId;
    public int SpawnIndex { get; } = spawnIndex;
    public CharacterBody2D Body { get; } = body;
    public PeerInputCounters Input { get; } = input;
    public Faction Faction => SpawnIndex == 0 ? Faction.DevBlue : Faction.DevRed;
    public int Health { get; set; } = CombatRules.DevPlayerMaxHealth;
    public WeaponDef Weapon { get; set; } = WeaponDef.DevHitscan;
    public uint FireRemaining { get; set; }
    public uint GrenadeRemaining { get; set; }
}

public readonly record struct PendingDamage(uint Source, Faction Faction, ServerPlayer Target, int Amount, float Distance, string Weapon);
