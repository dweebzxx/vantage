using Vantage.Shared.Game;
using Xunit;
namespace Vantage.Shared.Tests;

public sealed class CombatRulesTests
{
    [Fact]
    public void Dev_health_and_weapon_cooldowns_match_approved_placeholders()
    {
        Assert.Equal(100, CombatRules.DevPlayerMaxHealth);
        Assert.Equal(15u, CombatRules.CooldownTicks(WeaponDef.DevHitscan.FireIntervalSeconds));
        Assert.Equal(36u, CombatRules.CooldownTicks(WeaponDef.DevProjectile.FireIntervalSeconds));
        Assert.Equal(90u, CombatRules.CooldownTicks(GrenadeDef.DevPlaceholder.FuseSeconds));
        Assert.Equal(248f, CombatRules.MuzzleOffset + CombatRules.GrenadeTravelDistance);
    }

    [Theory]
    [InlineData(Faction.None, Faction.None, true)]
    [InlineData(Faction.None, Faction.DevBlue, true)]
    [InlineData(Faction.DevBlue, Faction.None, true)]
    [InlineData(Faction.DevBlue, Faction.DevBlue, false)]
    [InlineData(Faction.DevRed, Faction.DevRed, false)]
    [InlineData(Faction.DevBlue, Faction.DevRed, true)]
    [InlineData(Faction.DevRed, Faction.DevBlue, true)]
    [InlineData((Faction)99, Faction.DevRed, false)]
    [InlineData(Faction.DevBlue, (Faction)(-1), false)]
    public void Faction_rule_protects_same_non_none_faction_and_rejects_undefined_values(Faction attacker, Faction target, bool expected) =>
        Assert.Equal(expected, CombatRules.MayDamage(attacker, target));

    [Fact]
    public void Cooldown_rounds_up_zero_fractional_exact_and_largest_supported_ticks()
    {
        Assert.Equal(0u, CombatRules.CooldownTicks(0d));
        Assert.Equal(1u, CombatRules.CooldownTicks(0.001d));
        Assert.Equal(15u, CombatRules.CooldownTicks(0.25d));
        Assert.Equal(16u, CombatRules.CooldownTicks(0.25001d));
        Assert.Equal((uint)int.MaxValue, CombatRules.CooldownTicks(int.MaxValue / 60d));
        Assert.Equal(0u, CombatRules.CooldownTicks(0f));
        Assert.Equal(1u, CombatRules.CooldownTicks(0.001f));
        Assert.Equal(16u, CombatRules.CooldownTicks(0.25001f));
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(35791395)] // Beyond signed tick comparison window at 60 Hz.
    public void Cooldown_rejects_negative_nonfinite_and_overflow(double seconds)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CombatRules.CooldownTicks(seconds));
        Assert.Throws<ArgumentOutOfRangeException>(() => CombatRules.CooldownTicks((float)seconds));
    }
}
