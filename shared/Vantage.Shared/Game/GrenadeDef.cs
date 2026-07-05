namespace Vantage.Shared.Game;

/// <summary>
/// Immutable grenade tuning contract for pure shared combat math.
/// This does not define a final grenade roster.
/// </summary>
public readonly record struct GrenadeDef(
    float FuseSeconds,
    float RadiusUnits,
    int Damage,
    float Falloff)
{
    /// <summary>
    /// Development-only grenade placeholder for deterministic tests. Not final roster or balance.
    /// </summary>
    public static GrenadeDef DevPlaceholder { get; } = new(
        FuseSeconds: 1.5f,
        RadiusUnits: 96.0f,
        Damage: 60,
        Falloff: 1.0f);
}
