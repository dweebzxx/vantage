using Vantage.Shared.Net;

namespace Vantage.Shared.Game;

public static class CombatRules
{
    // Development placeholder, not final balance.
    public const int DevPlayerMaxHealth = 100;
    public const float MuzzleOffset = 28f;
    public const float ProjectileRadius = 6f;
    public const float GrenadeTravelDistance = 220f; // Beyond the muzzle: 248 from the player.
    public const float GrenadeTravelSpeed = 420f;

    public static bool MayDamage(Faction attacker, Faction target) =>
        Enum.IsDefined(attacker) && Enum.IsDefined(target) &&
        (attacker == Faction.None || attacker != target);

    // Preserve float tuning precision when converting existing weapon definitions.
    public static uint CooldownTicks(float seconds)
    {
        float ticks = MathF.Ceiling(seconds * TickMath.SimulationRateHz);
        if (!float.IsFinite(seconds) || seconds < 0 || ticks >= int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(seconds));
        return (uint)ticks;
    }

    public static uint CooldownTicks(double seconds)
    {
        double ticks = Math.Ceiling(seconds * TickMath.SimulationRateHz);
        if (!double.IsFinite(seconds) || seconds < 0 || ticks > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(seconds));
        return (uint)ticks;
    }
}
