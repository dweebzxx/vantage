namespace Vantage.Shared.Net;

public static class TickMath
{
    public const int SimulationRateHz = 60;

    public static double TicksToSeconds(uint ticks) => ticks / (double)SimulationRateHz;

    /// <summary>Floors nonnegative seconds to whole ticks; rejects values outside uint range.</summary>
    public static uint SecondsToWholeTicks(double seconds)
    {
        double ticks = Math.Floor(seconds * SimulationRateHz);
        if (!double.IsFinite(seconds) || seconds < 0.0 || ticks > uint.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(seconds));
        }

        return (uint)ticks;
    }

    // Serial-number comparisons are valid only when ticks are separated by strictly
    // fewer than 2^31 ticks (maximum valid magnitude: int.MaxValue).
    // The exact half-range separation is ambiguous and outside this contract.
    public static int Delta(uint tick, uint referenceTick) => unchecked((int)(tick - referenceTick));

    public static bool IsNewerThan(uint tick, uint referenceTick) => Delta(tick, referenceTick) > 0;
}
