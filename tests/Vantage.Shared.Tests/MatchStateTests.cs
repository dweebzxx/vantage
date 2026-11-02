using Vantage.Shared.Game;
using Xunit;
namespace Vantage.Shared.Tests;

public sealed class MatchStateTests
{
    private const uint Blue = 1;
    private const uint Red = 2;

    // Warmup 3, match 5, postgame 2 ticks.
    private static MatchState NewState() => new(new MatchTiming(3, 5, 2));

    private static MatchState NewStateWithTwoPlayers()
    {
        MatchState state = NewState();
        Assert.True(state.AddPlayer(Blue, Faction.DevBlue));
        Assert.True(state.AddPlayer(Red, Faction.DevRed));
        return state;
    }

    private static MatchTransition AssertValid(MatchTransition? transition)
    {
        Assert.NotNull(transition);
        Assert.True(MatchRules.IsValidTransition(transition.Value.From, transition.Value.To));
        return transition.Value;
    }

    // Calls Advance() `count` times and requires every call to return null.
    private static void AssertNoTransitionFor(MatchState state, int count)
    {
        for (int i = 1; i <= count; i++)
            Assert.True(state.Advance() is null, $"Advance() call {i} of {count} changed the phase to {state.Phase}.");
    }

    // Advances until the state reaches `target`, recording a kill on arrival at Playing when asked.
    private static void DriveTo(MatchState state, MatchPhase target, bool killWhenPlaying)
    {
        for (int i = 0; i < 100 && state.Phase != target; i++)
        {
            MatchTransition? transition = state.Advance();
            if (transition is { To: MatchPhase.Playing } && killWhenPlaying)
                Assert.True(state.RecordKill(Blue, Faction.DevBlue, Red, Faction.DevRed));
        }

        Assert.Equal(target, state.Phase);
    }

    [Fact]
    public void New_state_is_idle_with_no_time_and_default_timing()
    {
        var state = new MatchState();

        Assert.Equal(MatchPhase.Idle, state.Phase);
        Assert.Equal(0u, state.TicksRemaining);
        Assert.Equal(0, state.PlayerCount);
        Assert.Same(MatchTiming.Default, state.Timing);
    }

    [Fact]
    public void Idle_without_players_never_transitions()
    {
        MatchState state = NewState();

        AssertNoTransitionFor(state, 10);

        Assert.Equal(MatchPhase.Idle, state.Phase);
        Assert.Equal(0u, state.TicksRemaining);
    }

    [Fact]
    public void Lifecycle_transitions_happen_on_the_exact_advance_call()
    {
        MatchState state = NewStateWithTwoPlayers();

        // Idle to Warmup on the first call with a player.
        MatchTransition enterWarmup = AssertValid(state.Advance());
        Assert.Equal(new MatchTransition(MatchPhase.Idle, MatchPhase.Warmup), enterWarmup);
        Assert.Equal(3u, state.TicksRemaining);

        // Warmup lasts exactly 3 calls after the one that entered it.
        AssertNoTransitionFor(state, 2);
        Assert.Equal(MatchPhase.Warmup, state.Phase);
        Assert.Equal(1u, state.TicksRemaining);
        MatchTransition enterPlaying = AssertValid(state.Advance());
        Assert.Equal(new MatchTransition(MatchPhase.Warmup, MatchPhase.Playing), enterPlaying);
        Assert.Equal(5u, state.TicksRemaining);

        // Playing lasts exactly 5 calls.
        AssertNoTransitionFor(state, 4);
        Assert.Equal(MatchPhase.Playing, state.Phase);
        MatchTransition enterPostgame = AssertValid(state.Advance());
        Assert.Equal(new MatchTransition(MatchPhase.Playing, MatchPhase.Postgame), enterPostgame);
        Assert.Equal(2u, state.TicksRemaining);

        // Postgame lasts exactly 2 calls, then the next warmup.
        AssertNoTransitionFor(state, 1);
        Assert.Equal(MatchPhase.Postgame, state.Phase);
        MatchTransition nextWarmup = AssertValid(state.Advance());
        Assert.Equal(new MatchTransition(MatchPhase.Postgame, MatchPhase.Warmup), nextWarmup);
        Assert.Equal(3u, state.TicksRemaining);
    }

    [Fact]
    public void Postgame_to_warmup_resets_scores_to_zero_and_keeps_rows()
    {
        MatchState state = NewStateWithTwoPlayers();
        DriveTo(state, MatchPhase.Playing, killWhenPlaying: true);
        DriveTo(state, MatchPhase.Postgame, killWhenPlaying: false);
        // The result stays readable through postgame.
        Assert.Equal(1, state.Score.ScoreOf(Faction.DevBlue));
        Assert.Equal(MatchOutcome.BlueWins, state.Score.GetOutcome());

        DriveTo(state, MatchPhase.Warmup, killWhenPlaying: false);

        Assert.Equal(0, state.Score.ScoreOf(Faction.DevBlue));
        Assert.Equal(0, state.Score.ScoreOf(Faction.DevRed));
        Assert.Equal(new[] { new PlayerMatchStats(Blue, Faction.DevBlue, 0, 0), new PlayerMatchStats(Red, Faction.DevRed, 0, 0) },
            state.Score.Players);
        Assert.Equal(2, state.PlayerCount);
    }

    [Fact]
    public void Several_full_cycles_repeat_the_same_phase_lengths()
    {
        MatchState state = NewStateWithTwoPlayers();
        state.Advance(); // Idle to Warmup.

        for (int cycle = 0; cycle < 3; cycle++)
        {
            AssertNoTransitionFor(state, 2);
            Assert.Equal(new MatchTransition(MatchPhase.Warmup, MatchPhase.Playing), AssertValid(state.Advance()));
            AssertNoTransitionFor(state, 4);
            Assert.Equal(new MatchTransition(MatchPhase.Playing, MatchPhase.Postgame), AssertValid(state.Advance()));
            AssertNoTransitionFor(state, 1);
            Assert.Equal(new MatchTransition(MatchPhase.Postgame, MatchPhase.Warmup), AssertValid(state.Advance()));
        }
    }

    [Theory]
    [InlineData(MatchPhase.Warmup)]
    [InlineData(MatchPhase.Playing)]
    [InlineData(MatchPhase.Postgame)]
    public void Removing_the_last_player_returns_to_idle_and_clears_the_score(MatchPhase from)
    {
        MatchState state = NewStateWithTwoPlayers();
        DriveTo(state, from, killWhenPlaying: true);

        Assert.True(state.RemovePlayer(Blue));
        Assert.True(state.RemovePlayer(Red));
        MatchTransition toIdle = AssertValid(state.Advance());

        Assert.Equal(new MatchTransition(from, MatchPhase.Idle), toIdle);
        Assert.Equal(MatchPhase.Idle, state.Phase);
        Assert.Equal(0u, state.TicksRemaining);
        Assert.Equal(0, state.PlayerCount);
        Assert.Equal(0, state.Score.ScoreOf(Faction.DevBlue));
        Assert.Equal(0, state.Score.ScoreOf(Faction.DevRed));
        Assert.Null(state.Advance());
    }

    [Theory]
    [InlineData(MatchPhase.Warmup)]
    [InlineData(MatchPhase.Playing)]
    [InlineData(MatchPhase.Postgame)]
    public void Next_player_after_idle_starts_a_fresh_warmup_at_zero_zero(MatchPhase leftIn)
    {
        MatchState state = NewStateWithTwoPlayers();
        DriveTo(state, leftIn, killWhenPlaying: true);
        state.RemovePlayer(Blue);
        state.RemovePlayer(Red);
        state.Advance();

        Assert.True(state.AddPlayer(7, Faction.DevRed));
        MatchTransition enterWarmup = AssertValid(state.Advance());

        Assert.Equal(new MatchTransition(MatchPhase.Idle, MatchPhase.Warmup), enterWarmup);
        Assert.Equal(3u, state.TicksRemaining);
        Assert.Equal(0, state.Score.ScoreOf(Faction.DevBlue));
        Assert.Equal(0, state.Score.ScoreOf(Faction.DevRed));
        Assert.Equal(new[] { new PlayerMatchStats(7, Faction.DevRed, 0, 0) }, state.Score.Players);
    }

    [Fact]
    public void Match_with_one_team_empty_keeps_advancing_and_ends_normally()
    {
        MatchState state = NewState();
        state.AddPlayer(Blue, Faction.DevBlue); // Red team stays empty.

        Assert.Equal(new MatchTransition(MatchPhase.Idle, MatchPhase.Warmup), AssertValid(state.Advance()));
        AssertNoTransitionFor(state, 2);
        Assert.Equal(new MatchTransition(MatchPhase.Warmup, MatchPhase.Playing), AssertValid(state.Advance()));
        AssertNoTransitionFor(state, 4);
        Assert.Equal(new MatchTransition(MatchPhase.Playing, MatchPhase.Postgame), AssertValid(state.Advance()));
        Assert.Equal(MatchOutcome.Draw, state.Score.GetOutcome());
    }

    [Fact]
    public void A_team_emptying_mid_match_does_not_end_the_match()
    {
        MatchState state = NewStateWithTwoPlayers();
        DriveTo(state, MatchPhase.Playing, killWhenPlaying: true);

        state.RemovePlayer(Red);
        AssertNoTransitionFor(state, 4);

        Assert.Equal(MatchPhase.Playing, state.Phase);
        Assert.Equal(1, state.Score.ScoreOf(Faction.DevBlue));
        Assert.Equal(new MatchTransition(MatchPhase.Playing, MatchPhase.Postgame), AssertValid(state.Advance()));
    }

    [Fact]
    public void Valid_kill_scores_only_while_playing()
    {
        MatchState state = NewStateWithTwoPlayers();
        DriveTo(state, MatchPhase.Playing, killWhenPlaying: false);

        Assert.True(state.RecordKill(Blue, Faction.DevBlue, Red, Faction.DevRed));

        Assert.Equal(1, state.Score.ScoreOf(Faction.DevBlue));
        Assert.Equal(new PlayerMatchStats(Blue, Faction.DevBlue, 1, 0), state.Score.Players[0]);
        Assert.Equal(new PlayerMatchStats(Red, Faction.DevRed, 0, 1), state.Score.Players[1]);
    }

    [Theory]
    [InlineData(MatchPhase.Idle)]
    [InlineData(MatchPhase.Warmup)]
    [InlineData(MatchPhase.Postgame)]
    public void Valid_kill_outside_playing_changes_nothing_including_deaths(MatchPhase phase)
    {
        MatchState state = NewStateWithTwoPlayers();
        DriveTo(state, phase, killWhenPlaying: false);

        Assert.False(state.RecordKill(Blue, Faction.DevBlue, Red, Faction.DevRed));
        Assert.False(state.RecordKill(Red, Faction.DevRed, Blue, Faction.DevBlue));

        Assert.Equal(0, state.Score.ScoreOf(Faction.DevBlue));
        Assert.Equal(0, state.Score.ScoreOf(Faction.DevRed));
        Assert.Equal(new[] { new PlayerMatchStats(Blue, Faction.DevBlue, 0, 0), new PlayerMatchStats(Red, Faction.DevRed, 0, 0) },
            state.Score.Players);
    }

    [Fact]
    public void Invalid_kill_during_playing_is_still_rejected()
    {
        MatchState state = NewStateWithTwoPlayers();
        state.AddPlayer(3, Faction.DevBlue);
        DriveTo(state, MatchPhase.Playing, killWhenPlaying: false);

        Assert.False(state.RecordKill(Blue, Faction.DevBlue, 3, Faction.DevBlue));
        Assert.False(state.RecordKill(Blue, Faction.DevBlue, Blue, Faction.DevRed));
        Assert.False(state.RecordKill(Blue, Faction.None, Red, Faction.DevRed));

        Assert.Equal(0, state.Score.ScoreOf(Faction.DevBlue));
        Assert.Equal(0, state.Score.ScoreOf(Faction.DevRed));
    }

    [Fact]
    public void Kill_by_a_player_who_left_still_scores_during_playing()
    {
        MatchState state = NewStateWithTwoPlayers();
        DriveTo(state, MatchPhase.Playing, killWhenPlaying: false);
        state.RemovePlayer(Blue);

        Assert.True(state.RecordKill(Blue, Faction.DevBlue, Red, Faction.DevRed));

        Assert.Equal(1, state.Score.ScoreOf(Faction.DevBlue));
        Assert.Equal(1, state.PlayerCount);
    }

    [Fact]
    public void Add_and_remove_pass_through_to_the_score()
    {
        MatchState state = NewState();

        Assert.True(state.AddPlayer(5, Faction.DevRed));
        Assert.False(state.AddPlayer(5, Faction.DevBlue));
        Assert.False(state.AddPlayer(6, Faction.None));
        Assert.Equal(1, state.PlayerCount);
        Assert.False(state.RemovePlayer(99));
        Assert.True(state.RemovePlayer(5));
        Assert.Equal(0, state.PlayerCount);
    }

    [Fact]
    public void New_players_are_assigned_to_the_smaller_team_and_blue_on_ties()
    {
        MatchState state = NewState();
        var assigned = new List<Faction>();

        for (uint id = 1; id <= 5; id++)
        {
            Faction team = state.ChooseTeamForNewPlayer();
            assigned.Add(team);
            state.AddPlayer(id, team);
        }

        Assert.Equal(
            new[] { Faction.DevBlue, Faction.DevRed, Faction.DevBlue, Faction.DevRed, Faction.DevBlue },
            assigned);
    }

    [Fact]
    public void Late_joiner_goes_to_the_team_with_fewer_players_after_a_departure()
    {
        MatchState state = NewState();
        state.AddPlayer(1, Faction.DevBlue);
        state.AddPlayer(2, Faction.DevBlue);
        state.AddPlayer(3, Faction.DevRed);
        state.RemovePlayer(3); // Blue 2, red 0.

        Assert.Equal(Faction.DevRed, state.ChooseTeamForNewPlayer());
    }
}
