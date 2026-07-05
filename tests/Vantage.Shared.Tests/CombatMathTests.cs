using Vantage.Shared.Game;
using Xunit;

namespace Vantage.Shared.Tests;

public sealed class CombatMathTests
{
    [Fact]
    public void Hitscan_damage_returns_full_damage_at_zero_distance()
    {
        WeaponDef weapon = WeaponDef.DevHitscan;

        int damage = DamageMath.HitscanDamage(weapon, 0.0f);

        Assert.Equal(weapon.Damage, damage);
    }

    [Fact]
    public void Hitscan_damage_returns_full_damage_at_exact_max_range()
    {
        WeaponDef weapon = WeaponDef.DevHitscan;

        int damage = DamageMath.HitscanDamage(weapon, weapon.RangeUnits);

        Assert.Equal(weapon.Damage, damage);
    }

    [Fact]
    public void Hitscan_damage_returns_zero_beyond_max_range()
    {
        WeaponDef weapon = WeaponDef.DevHitscan;

        int damage = DamageMath.HitscanDamage(weapon, weapon.RangeUnits + 0.01f);

        Assert.Equal(0, damage);
    }

    [Fact]
    public void Hitscan_damage_treats_negative_distance_as_zero()
    {
        int damage = DamageMath.DamageByRange(damage: 10, distanceUnits: -5.0f, maxRangeUnits: 100.0f);

        Assert.Equal(10, damage);
    }

    [Fact]
    public void Hitscan_damage_clamps_negative_damage_to_zero()
    {
        int damage = DamageMath.DamageByRange(damage: -10, distanceUnits: 0.0f, maxRangeUnits: 100.0f);

        Assert.Equal(0, damage);
    }

    [Fact]
    public void Radius_damage_returns_full_damage_at_center()
    {
        int damage = DamageMath.RadiusDamage(damage: 100, distanceUnits: 0.0f, radiusUnits: 10.0f, falloff: 1.0f);

        Assert.Equal(100, damage);
    }

    [Fact]
    public void Radius_damage_treats_negative_distance_as_center()
    {
        int damage = DamageMath.RadiusDamage(damage: 100, distanceUnits: -4.0f, radiusUnits: 10.0f, falloff: 1.0f);

        Assert.Equal(100, damage);
    }

    [Fact]
    public void Radius_damage_returns_zero_at_edge_with_full_falloff()
    {
        int damage = DamageMath.RadiusDamage(damage: 100, distanceUnits: 10.0f, radiusUnits: 10.0f, falloff: 1.0f);

        Assert.Equal(0, damage);
    }

    [Fact]
    public void Radius_damage_returns_zero_outside_radius()
    {
        int damage = DamageMath.RadiusDamage(damage: 100, distanceUnits: 10.01f, radiusUnits: 10.0f, falloff: 1.0f);

        Assert.Equal(0, damage);
    }

    [Fact]
    public void Radius_damage_clamps_negative_damage_to_zero()
    {
        int damage = DamageMath.RadiusDamage(damage: -100, distanceUnits: 0.0f, radiusUnits: 10.0f, falloff: 1.0f);

        Assert.Equal(0, damage);
    }

    [Fact]
    public void Apply_damage_to_health_never_underflows_below_zero()
    {
        int health = DamageMath.ApplyDamageToHealth(currentHealth: 20, damage: 25);

        Assert.Equal(0, health);
    }

    [Fact]
    public void Apply_damage_to_health_clamps_negative_inputs()
    {
        Assert.Equal(0, DamageMath.ApplyDamageToHealth(currentHealth: -5, damage: 1));
        Assert.Equal(20, DamageMath.ApplyDamageToHealth(currentHealth: 20, damage: -5));
    }

    [Fact]
    public void Dev_weapon_definitions_are_usable_placeholders()
    {
        WeaponDef hitscan = WeaponDef.DevHitscan;
        WeaponDef projectile = WeaponDef.DevProjectile;

        Assert.StartsWith("Dev", nameof(WeaponDef.DevHitscan));
        Assert.StartsWith("Dev", nameof(WeaponDef.DevProjectile));
        Assert.StartsWith("Dev", hitscan.Id.ToString());
        Assert.StartsWith("Dev", projectile.Id.ToString());

        Assert.Equal(WeaponId.DevHitscan, hitscan.Id);
        Assert.Equal(WeaponKind.Hitscan, hitscan.Kind);
        Assert.True(hitscan.RangeUnits > 0.0f);
        Assert.True(hitscan.Damage > 0);
        Assert.True(hitscan.FireIntervalSeconds > 0.0f);
        Assert.True(hitscan.MagazineSize > 0);
        Assert.Equal(0.0f, hitscan.ProjectileSpeedUnitsPerSecond);

        Assert.Equal(WeaponId.DevProjectile, projectile.Id);
        Assert.Equal(WeaponKind.Projectile, projectile.Kind);
        Assert.True(projectile.RangeUnits > 0.0f);
        Assert.True(projectile.Damage > 0);
        Assert.True(projectile.FireIntervalSeconds > 0.0f);
        Assert.True(projectile.MagazineSize > 0);
        Assert.True(projectile.ProjectileSpeedUnitsPerSecond > 0.0f);
    }

    [Fact]
    public void Dev_grenade_placeholder_is_usable_for_radius_math()
    {
        GrenadeDef grenade = GrenadeDef.DevPlaceholder;

        Assert.StartsWith("Dev", nameof(GrenadeDef.DevPlaceholder));
        Assert.True(grenade.FuseSeconds > 0.0f);
        Assert.True(grenade.RadiusUnits > 0.0f);
        Assert.True(grenade.Damage > 0);
        Assert.InRange(grenade.Falloff, 0.0f, 1.0f);
        Assert.Equal(grenade.Damage, DamageMath.RadiusDamage(grenade, 0.0f));
    }
}
