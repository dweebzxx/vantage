using Vantage.Shared.Game;
using Xunit;

namespace Vantage.Shared.Tests;

public sealed class MatchClockTests
{
    [Theory]
    [InlineData(1000u, 1000u, 0)]      // 0 ticks left
    [InlineData(1001u, 1000u, 1)]      // 1 tick rounds up to 1 s
    [InlineData(1060u, 1000u, 1)]      // exactly 60 ticks is 1 s
    [InlineData(1061u, 1000u, 2)]      // 61 ticks rounds up to 2 s
    [InlineData(19000u, 1000u, 300)]   // 18000 ticks is 5 minutes
    [InlineData(1000u, 1001u, 0)]      // the end passed by 1 tick
    [InlineData(1000u, 5000u, 0)]      // the end passed long ago
    public void SecondsLeft_rounds_up_and_never_goes_below_zero(uint end, uint now, int expected) =>
        Assert.Equal(expected, MatchClock.SecondsLeft(end, now));

    [Fact]
    public void SecondsLeft_is_wrap_safe_when_the_end_is_just_after_a_tick_wrap()
    {
        // Now is 30 ticks before the wrap and the end is 90 ticks after it: 120 ticks, so 2 s.
        uint now = uint.MaxValue - 29u;
        uint end = 89u;
        Assert.Equal(2, MatchClock.SecondsLeft(end, now));
    }

    [Fact]
    public void SecondsLeft_is_zero_when_now_has_wrapped_past_the_end()
    {
        // The end is 30 ticks before the wrap; now is 10 ticks after it.
        Assert.Equal(0, MatchClock.SecondsLeft(uint.MaxValue - 29u, 9u));
    }

    [Theory]
    [InlineData(0, "0:00")]
    [InlineData(5, "0:05")]
    [InlineData(59, "0:59")]
    [InlineData(60, "1:00")]
    [InlineData(300, "5:00")]
    [InlineData(3599, "59:59")]
    [InlineData(6000, "100:00")]
    [InlineData(-1, "0:00")]
    [InlineData(int.MinValue, "0:00")]
    public void Format_writes_minutes_and_two_digit_seconds(int seconds, string expected) =>
        Assert.Equal(expected, MatchClock.Format(seconds));
}
