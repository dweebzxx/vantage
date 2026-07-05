namespace Vantage.Shared.Game;

public static class DamageMath
{
    public static int HitscanDamage(WeaponDef weapon, float distanceUnits)
    {
        return DamageByRange(weapon.Damage, distanceUnits, weapon.RangeUnits);
    }

    public static int DamageByRange(int damage, float distanceUnits, float maxRangeUnits)
    {
        int safeDamage = ClampDamage(damage);
        if (safeDamage == 0)
        {
            return 0;
        }

        float safeRange = ClampLimit(maxRangeUnits);
        if (safeRange <= 0.0f)
        {
            return 0;
        }

        float safeDistance = ClampDistance(distanceUnits);
        return safeDistance <= safeRange ? safeDamage : 0;
    }

    public static int RadiusDamage(GrenadeDef grenade, float distanceUnits)
    {
        return RadiusDamage(grenade.Damage, distanceUnits, grenade.RadiusUnits, grenade.Falloff);
    }

    public static int RadiusDamage(int damage, float distanceUnits, float radiusUnits, float falloff)
    {
        int safeDamage = ClampDamage(damage);
        if (safeDamage == 0)
        {
            return 0;
        }

        float safeRadius = ClampLimit(radiusUnits);
        if (safeRadius <= 0.0f)
        {
            return 0;
        }

        float safeDistance = ClampDistance(distanceUnits);
        if (safeDistance > safeRadius)
        {
            return 0;
        }

        float safeFalloff = ClampFalloff(falloff);
        float distanceRatio = safeDistance / safeRadius;
        float multiplier = 1.0f - (distanceRatio * safeFalloff);

        return ClampDamage((int)MathF.Round(safeDamage * MathF.Max(0.0f, multiplier), MidpointRounding.AwayFromZero));
    }

    public static int ApplyDamageToHealth(int currentHealth, int damage)
    {
        int safeHealth = Math.Max(0, currentHealth);
        int safeDamage = ClampDamage(damage);

        if (safeDamage >= safeHealth)
        {
            return 0;
        }

        return safeHealth - safeDamage;
    }

    private static int ClampDamage(int damage)
    {
        return Math.Max(0, damage);
    }

    private static float ClampDistance(float distanceUnits)
    {
        if (float.IsNaN(distanceUnits) || float.IsPositiveInfinity(distanceUnits))
        {
            return float.PositiveInfinity;
        }

        if (float.IsNegativeInfinity(distanceUnits))
        {
            return 0.0f;
        }

        return MathF.Max(0.0f, distanceUnits);
    }

    private static float ClampLimit(float units)
    {
        return float.IsFinite(units) ? MathF.Max(0.0f, units) : 0.0f;
    }

    private static float ClampFalloff(float falloff)
    {
        if (!float.IsFinite(falloff))
        {
            return 1.0f;
        }

        return Math.Clamp(falloff, 0.0f, 1.0f);
    }
}
