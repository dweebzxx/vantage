using Vantage.Shared.Net;

namespace Vantage.Shared.Game;

/// <summary>Phase durations in simulation ticks. Every duration must be at least one tick.</summary>
public sealed record MatchTiming
{
    public static MatchTiming Default { get; } = new(
        TickMath.SecondsToWholeTicks(MatchRules.WarmupSeconds),
        TickMath.SecondsToWholeTicks(MatchRules.MatchSeconds),
        TickMath.SecondsToWholeTicks(MatchRules.PostgameSeconds));

    public uint WarmupTicks { get; }
    public uint PlayingTicks { get; }
    public uint PostgameTicks { get; }

    public MatchTiming(uint warmupTicks, uint playingTicks, uint postgameTicks)
    {
        ArgumentOutOfRangeException.ThrowIfZero(warmupTicks);
        ArgumentOutOfRangeException.ThrowIfZero(playingTicks);
        ArgumentOutOfRangeException.ThrowIfZero(postgameTicks);
        WarmupTicks = warmupTicks;
        PlayingTicks = playingTicks;
        PostgameTicks = postgameTicks;
    }
}
