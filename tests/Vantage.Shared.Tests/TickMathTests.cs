using Vantage.Shared.Net;
using Xunit;

namespace Vantage.Shared.Tests;

public sealed class TickMathTests
{
    [Fact]
    public void Tick_conversion_uses_sixty_hz_and_floors_fractional_ticks()
    {
        Assert.Equal(60, TickMath.SimulationRateHz);
        Assert.Equal(0.0, TickMath.TicksToSeconds(0u));
        Assert.Equal(1.5, TickMath.TicksToSeconds(90u));
        Assert.Equal(0u, TickMath.SecondsToWholeTicks(0.0));
        Assert.Equal(90u, TickMath.SecondsToWholeTicks(1.5));
        Assert.Equal(90u, TickMath.SecondsToWholeTicks(1.51));
        Assert.Equal(uint.MaxValue, TickMath.SecondsToWholeTicks(TickMath.TicksToSeconds(uint.MaxValue)));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(-0.01)]
    [InlineData(71582788.26666667)] // 2^32 ticks / 60: first overflowing whole tick.
    public void Seconds_to_ticks_rejects_invalid_or_overflowing_values(double seconds)
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => TickMath.SecondsToWholeTicks(seconds));

        Assert.Equal("seconds", exception.ParamName);
    }

    [Fact]
    public void Tick_delta_and_order_handle_equal_ticks()
    {
        Assert.Equal(0, TickMath.Delta(uint.MaxValue, uint.MaxValue));
        Assert.False(TickMath.IsNewerThan(uint.MaxValue, uint.MaxValue));
        Assert.Equal(0, TickMath.Delta(0u, 0u));
        Assert.False(TickMath.IsNewerThan(0u, 0u));
    }

    [Fact]
    public void Tick_delta_and_order_handle_ordinary_ticks()
    {
        Assert.Equal(7, TickMath.Delta(17u, 10u));
        Assert.Equal(-7, TickMath.Delta(10u, 17u));
        Assert.True(TickMath.IsNewerThan(17u, 10u));
        Assert.False(TickMath.IsNewerThan(10u, 17u));
    }

    [Fact]
    public void Tick_delta_and_order_cross_uint_wraparound()
    {
        Assert.Equal(1, TickMath.Delta(0u, uint.MaxValue));
        Assert.Equal(-1, TickMath.Delta(uint.MaxValue, 0u));
        Assert.True(TickMath.IsNewerThan(0u, uint.MaxValue));
        Assert.False(TickMath.IsNewerThan(uint.MaxValue, 0u));
        Assert.Equal(5, TickMath.Delta(2u, uint.MaxValue - 2u));
        Assert.Equal(-5, TickMath.Delta(uint.MaxValue - 2u, 2u));
    }

    [Fact]
    public void Tick_delta_and_order_handle_largest_valid_separation()
    {
        Assert.Equal(int.MaxValue, TickMath.Delta(int.MaxValue, 0u));
        Assert.Equal(-int.MaxValue, TickMath.Delta(0u, int.MaxValue));
        Assert.True(TickMath.IsNewerThan(int.MaxValue, 0u));
        Assert.False(TickMath.IsNewerThan(0u, int.MaxValue));
        Assert.Equal(int.MaxValue, TickMath.Delta(0u, 2147483649u));
        Assert.Equal(-int.MaxValue, TickMath.Delta(2147483649u, 0u));
        Assert.True(TickMath.IsNewerThan(0u, 2147483649u));
        Assert.False(TickMath.IsNewerThan(2147483649u, 0u));
    }
}
