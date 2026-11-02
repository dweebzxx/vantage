using System.Globalization;
using Vantage.Shared.Net;

namespace Vantage.Shared.Game;

/// <summary>
/// Pure clock helpers for showing the time left in a match phase. They hold no state and decide no match rule.
/// </summary>
public static class MatchClock
{
    /// <summary>
    /// The whole seconds from <paramref name="currentTick"/> to <paramref name="phaseEndTick"/>, rounded up.
    /// Returns 0 when the end is at or before the current tick. Wrap-safe through <see cref="TickMath.Delta"/>.
    /// </summary>
    public static int SecondsLeft(uint phaseEndTick, uint currentTick)
    {
        int ticksLeft = TickMath.Delta(phaseEndTick, currentTick);
        if (ticksLeft <= 0) return 0;
        // Widen before adding so a very long phase cannot overflow int.
        return (int)(((long)ticksLeft + TickMath.SimulationRateHz - 1) / TickMath.SimulationRateHz);
    }

    /// <summary>
    /// Formats whole seconds as <c>m:ss</c>. Minutes are not capped and seconds have two digits.
    /// A negative value gives <c>0:00</c>.
    /// </summary>
    public static string Format(int seconds)
    {
        if (seconds < 0) seconds = 0;
        return string.Create(CultureInfo.InvariantCulture, $"{seconds / 60}:{seconds % 60:00}");
    }
}
