namespace Vantage.Shared.Game;

/// <summary>
/// Immutable weapon tuning contract shared by local tests and future client/server combat code.
/// Values exposed here are development placeholders unless explicitly promoted in a later pass.
/// </summary>
public readonly record struct WeaponDef(
    WeaponId Id,
    WeaponKind Kind,
    float RangeUnits,
    int Damage,
    float FireIntervalSeconds,
    int MagazineSize,
    float ProjectileSpeedUnitsPerSecond)
{
    /// <summary>
    /// Development-only hitscan placeholder. Not final roster or balance.
    /// </summary>
    public static WeaponDef DevHitscan { get; } = new(
        WeaponId.DevHitscan,
        WeaponKind.Hitscan,
        RangeUnits: 320.0f,
        Damage: 25,
        FireIntervalSeconds: 0.25f,
        MagazineSize: 12,
        ProjectileSpeedUnitsPerSecond: 0.0f);

    /// <summary>
    /// Development-only projectile placeholder. Not final roster or balance.
    /// </summary>
    public static WeaponDef DevProjectile { get; } = new(
        WeaponId.DevProjectile,
        WeaponKind.Projectile,
        RangeUnits: 480.0f,
        Damage: 40,
        FireIntervalSeconds: 0.6f,
        MagazineSize: 4,
        ProjectileSpeedUnitsPerSecond: 560.0f);
}
