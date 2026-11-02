using Godot;
using Vantage.Shared.Game;
using Vantage.Shared.Protocol;
namespace Vantage.Server;

public static class DamageSystem
{
    public static void Resolve(uint tick, IEnumerable<PendingDamage> damage, List<byte[]> events, MatchHost match, Func<ServerPlayer, Vector2> respawnPosition)
    {
        foreach (var hit in damage)
        {
            var target = hit.Target;
            if (hit.Source == target.EntityId || !CombatRules.MayDamage(hit.Faction, target.Faction) || hit.Amount <= 0) continue;
            target.Health = DamageMath.ApplyDamageToHealth(target.Health, hit.Amount);
            events.Add(MessageCodec.EncodeHit(new(tick, hit.Source, target.EntityId)));
            events.Add(MessageCodec.EncodeDamage(new(tick, hit.Source, target.EntityId, hit.Amount)));
            GD.Print($"[Combat] damage tick={tick}; source={hit.Source}; target={target.EntityId}; weapon={hit.Weapon}; distance={hit.Distance}; amount={hit.Amount}; health={target.Health}");
            if (target.Health != 0) continue;
            events.Add(MessageCodec.EncodeDefeat(new(tick, hit.Source, target.EntityId)));
            GD.Print($"[Combat] defeat tick={tick}; source={hit.Source}; target={target.EntityId}");
            match.RecordKill(tick, hit.Source, hit.Faction, target.EntityId, target.Faction);
            target.Health =CombatRules.DevPlayerMaxHealth;
            target.Body.GlobalPosition = respawnPosition(target);
            target.Body.Velocity = Vector2.Zero;
            target.Weapon = WeaponDef.DevHitscan;
            target.FireRemaining = target.GrenadeRemaining = 0;
            target.Input.Presses.ClearPending();
            GD.Print($"[Combat] respawn tick={tick}; entity={target.EntityId}; health={target.Health}; position={target.Body.GlobalPosition}");
        }
    }
}
