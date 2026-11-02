using Vantage.Shared.Game;
using Xunit;
namespace Vantage.Shared.Tests;

public sealed class MatchRulesTests
{
    private const MatchPhase Undefined = (MatchPhase)99;

    private static readonly MatchPhase[] AllPhases =
    {
        MatchPhase.Idle, MatchPhase.Warmup, MatchPhase.Playing, MatchPhase.Postgame, Undefined,
    };

    // Written out independently of the implementation: exactly the seven pairs of R2.
    private static readonly HashSet<(MatchPhase, MatchPhase)> ValidPairs = new()
    {
        (MatchPhase.Idle, MatchPhase.Warmup),
        (MatchPhase.Warmup, MatchPhase.Playing),
        (MatchPhase.Playing, MatchPhase.Postgame),
        (MatchPhase.Postgame, MatchPhase.Warmup),
        (MatchPhase.Warmup, MatchPhase.Idle),
        (MatchPhase.Playing, MatchPhase.Idle),
        (MatchPhase.Postgame, MatchPhase.Idle),
    };

    public static TheoryData<MatchPhase, MatchPhase, bool> TransitionPairs()
    {
        var data = new TheoryData<MatchPhase, MatchPhase, bool>();
        foreach (MatchPhase from in AllPhases)
        {
            foreach (MatchPhase to in AllPhases)
                data.Add(from, to, ValidPairs.Contains((from, to)));
        }

        return data;
    }

    [Fact]
    public void Phase_values_are_byte_backed_and_fixed()
    {
        Assert.Equal(typeof(byte), Enum.GetUnderlyingType(typeof(MatchPhase)));
        Assert.Equal(0, (byte)MatchPhase.Idle);
        Assert.Equal(1, (byte)MatchPhase.Warmup);
        Assert.Equal(2, (byte)MatchPhase.Playing);
        Assert.Equal(3, (byte)MatchPhase.Postgame);
    }

    [Theory]
    [MemberData(nameof(TransitionPairs))]
    public void Transition_table_allows_exactly_the_seven_listed_pairs(MatchPhase from, MatchPhase to, bool expected) =>
        Assert.Equal(expected, MatchRules.IsValidTransition(from, to));

    [Fact]
    public void Transition_table_has_exactly_seven_valid_pairs()
    {
        int valid = 0;
        foreach (MatchPhase from in AllPhases)
        {
            foreach (MatchPhase to in AllPhases)
            {
                if (MatchRules.IsValidTransition(from, to))
                    valid++;
            }
        }

        Assert.Equal(7, valid);
    }

    [Fact]
    public void Default_timing_is_ten_seconds_warmup_five_minute_match_and_fifteen_seconds_postgame()
    {
        Assert.Equal(10, MatchRules.WarmupSeconds);
        Assert.Equal(300, MatchRules.MatchSeconds);
        Assert.Equal(15, MatchRules.PostgameSeconds);
        Assert.Equal(600u, MatchTiming.Default.WarmupTicks);
        Assert.Equal(18000u, MatchTiming.Default.PlayingTicks);
        Assert.Equal(900u, MatchTiming.Default.PostgameTicks);
    }

    [Fact]
    public void Custom_timing_keeps_the_given_ticks()
    {
        var timing = new MatchTiming(3, 5, 2);

        Assert.Equal(3u, timing.WarmupTicks);
        Assert.Equal(5u, timing.PlayingTicks);
        Assert.Equal(2u, timing.PostgameTicks);
    }

    [Theory]
    [InlineData(0u, 5u, 2u, "warmupTicks")]
    [InlineData(3u, 0u, 2u, "playingTicks")]
    [InlineData(3u, 5u, 0u, "postgameTicks")]
    public void Timing_rejects_a_zero_duration(uint warmup, uint playing, uint postgame, string parameter)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new MatchTiming(warmup, playing, postgame));
        Assert.Equal(parameter, ex.ParamName);
    }

    [Theory]
    [InlineData(Faction.DevBlue, true)]
    [InlineData(Faction.DevRed, true)]
    [InlineData(Faction.None, false)]
    [InlineData((Faction)99, false)]
    [InlineData((Faction)(-1), false)]
    public void Only_the_two_dev_factions_are_teams(Faction faction, bool expected) =>
        Assert.Equal(expected, MatchRules.IsTeam(faction));
}
