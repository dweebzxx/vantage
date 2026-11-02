using Vantage.Shared.Game;
using Xunit;
namespace Vantage.Shared.Tests;

public sealed class MatchStateRebalanceTests
{
    // Warmup 3, match 5, postgame 2 ticks.
    private static MatchState NewState() => new(new MatchTiming(3, 5, 2));

    // Three blue and one red: imbalanced by 2. Entities 1, 3, 4 are blue; entity 2 is red.
    // The rebalance must move entity 4 (the highest blue id) to red.
    private static MatchState NewImbalancedState()
    {
        MatchState state = NewState();
        Assert.True(state.AddPlayer(1, Faction.DevBlue));
        Assert.True(state.AddPlayer(2, Faction.DevRed));
        Assert.True(state.AddPlayer(3, Faction.DevBlue));
        Assert.True(state.AddPlayer(4, Faction.DevBlue));
        return state;
    }

    private static MatchState NewBalancedState()
    {
        MatchState state = NewState();
        Assert.True(state.AddPlayer(1, Faction.DevBlue));
        Assert.True(state.AddPlayer(2, Faction.DevRed));
        Assert.True(state.AddPlayer(3, Faction.DevBlue));
        Assert.True(state.AddPlayer(4, Faction.DevRed));
        return state;
    }

    private static (uint EntityId, Faction Team)[] Teams(MatchState state) =>
        state.Score.Players.Select(p => (p.EntityId, p.Team)).ToArray();

    // Advances until the next call would leave Postgame (Postgame with one tick left), asserting LastTeamMoves is
    // empty on every call and the teams never change. Scores a kill (1 kills 2) when Playing starts.
    private static void AdvanceUntilBefore(MatchState state, MatchTransition transition, (uint, Faction)[] expectedTeams)
    {
        Assert.Equal(MatchPhase.Postgame, transition.From);
        for (int i = 0; i < 100; i++)
        {
            if (state.Phase == MatchPhase.Postgame && state.TicksRemaining == 1)
                return;

            MatchTransition? made = state.Advance();
            Assert.Empty(state.LastTeamMoves);
            Assert.Equal(expectedTeams, Teams(state));
            if (made is { To: MatchPhase.Playing })
                Assert.True(state.RecordKill(1, Faction.DevBlue, 2, Faction.DevRed));
        }

        Assert.Fail($"Never reached the call that makes {transition}.");
    }

    [Fact]
    public void New_state_has_no_team_moves() => Assert.Empty(new MatchState().LastTeamMoves);

    [Fact]
    public void Postgame_to_warmup_rebalances_resets_the_score_and_lists_the_moves()
    {
        MatchState state = NewImbalancedState();
        (uint, Faction)[] startTeams = Teams(state);
        AdvanceUntilBefore(state, new MatchTransition(MatchPhase.Postgame, MatchPhase.Warmup), startTeams);
        Assert.Equal(MatchPhase.Postgame, state.Phase);
        Assert.Equal(1, state.Score.BlueScore);
        Assert.Equal(1, state.Score.Players[0].Kills);

        MatchTransition? made = state.Advance();

        Assert.Equal(new MatchTransition(MatchPhase.Postgame, MatchPhase.Warmup), made);
        Assert.Equal(3u, state.TicksRemaining);
        Assert.Equal([new TeamMove(4, Faction.DevBlue, Faction.DevRed)], state.LastTeamMoves);
        Assert.Equal(0, state.Score.BlueScore);
        Assert.Equal(0, state.Score.RedScore);
        Assert.Equal(
        [
            new PlayerMatchStats(1, Faction.DevBlue, 0, 0),
            new PlayerMatchStats(2, Faction.DevRed, 0, 0),
            new PlayerMatchStats(3, Faction.DevBlue, 0, 0),
            new PlayerMatchStats(4, Faction.DevRed, 0, 0),
        ], state.Score.Players);
        Assert.Equal(2, state.Score.PlayerCountOf(Faction.DevBlue));
        Assert.Equal(2, state.Score.PlayerCountOf(Faction.DevRed));
    }

    [Fact]
    public void Team_moves_are_cleared_on_the_very_next_advance_call()
    {
        MatchState state = NewImbalancedState();
        AdvanceUntilBefore(state, new MatchTransition(MatchPhase.Postgame, MatchPhase.Warmup), Teams(state));
        Assert.NotNull(state.Advance());
        Assert.Single(state.LastTeamMoves);

        MatchTransition? next = state.Advance();

        Assert.Null(next);
        Assert.Empty(state.LastTeamMoves);
        Assert.Equal(2, state.Score.PlayerCountOf(Faction.DevBlue));
        Assert.Equal(2, state.Score.PlayerCountOf(Faction.DevRed));
    }

    [Fact]
    public void Balanced_roster_gives_no_moves_on_postgame_to_warmup()
    {
        MatchState state = NewBalancedState();
        (uint, Faction)[] startTeams = Teams(state);
        AdvanceUntilBefore(state, new MatchTransition(MatchPhase.Postgame, MatchPhase.Warmup), startTeams);

        MatchTransition? made = state.Advance();

        Assert.Equal(new MatchTransition(MatchPhase.Postgame, MatchPhase.Warmup), made);
        Assert.Empty(state.LastTeamMoves);
        Assert.Equal(startTeams, Teams(state));
    }

    [Fact]
    public void Roster_one_apart_gives_no_moves_on_postgame_to_warmup()
    {
        MatchState state = NewState();
        Assert.True(state.AddPlayer(1, Faction.DevBlue));
        Assert.True(state.AddPlayer(2, Faction.DevBlue));
        Assert.True(state.AddPlayer(3, Faction.DevRed));
        (uint, Faction)[] startTeams = Teams(state);
        AdvanceUntilBefore(state, new MatchTransition(MatchPhase.Postgame, MatchPhase.Warmup), startTeams);

        Assert.NotNull(state.Advance());

        Assert.Empty(state.LastTeamMoves);
        Assert.Equal(startTeams, Teams(state));
    }

    [Fact]
    public void Nobody_is_rebalanced_on_idle_warmup_playing_or_postgame_even_when_imbalanced()
    {
        MatchState state = NewImbalancedState();
        (uint, Faction)[] startTeams = Teams(state);
        var seen = new List<MatchTransition>();

        // Idle to Warmup, Warmup to Playing, Playing to Postgame, plus every tick in between and in Postgame.
        for (int i = 0; i < 100 && seen.Count < 3; i++)
        {
            MatchTransition? made = state.Advance();
            if (made is { } t)
                seen.Add(t);
            Assert.Empty(state.LastTeamMoves);
            Assert.Equal(startTeams, Teams(state));
        }

        Assert.Equal(
        [
            new MatchTransition(MatchPhase.Idle, MatchPhase.Warmup),
            new MatchTransition(MatchPhase.Warmup, MatchPhase.Playing),
            new MatchTransition(MatchPhase.Playing, MatchPhase.Postgame),
        ], seen);
        Assert.Null(state.Advance());
        Assert.Empty(state.LastTeamMoves);
        Assert.Equal(startTeams, Teams(state));
    }

    [Fact]
    public void Players_joining_mid_match_are_not_moved_until_the_next_match_starts()
    {
        // Start balanced, then add two more blue players during Playing, then one during Postgame.
        MatchState state = NewBalancedState();
        for (int i = 0; i < 100 && state.Phase != MatchPhase.Playing; i++)
            state.Advance();
        Assert.Equal(MatchPhase.Playing, state.Phase);
        Assert.True(state.AddPlayer(5, Faction.DevBlue));
        Assert.True(state.AddPlayer(6, Faction.DevBlue));
        Assert.Null(state.Advance());
        Assert.Empty(state.LastTeamMoves);
        Assert.Equal(4, state.Score.PlayerCountOf(Faction.DevBlue));
        Assert.Equal(2, state.Score.PlayerCountOf(Faction.DevRed));

        for (int i = 0; i < 100 && state.Phase != MatchPhase.Postgame; i++)
        {
            state.Advance();
            Assert.Empty(state.LastTeamMoves);
        }

        Assert.Equal(MatchPhase.Postgame, state.Phase);
        Assert.Equal(4, state.Score.PlayerCountOf(Faction.DevBlue));
        Assert.Equal(2, state.Score.PlayerCountOf(Faction.DevRed));

        Assert.Null(state.Advance());
        MatchTransition? made = state.Advance();

        // Blue 1, 3, 5, 6 and red 2, 4: one move, the latest blue joiner (6).
        Assert.Equal(new MatchTransition(MatchPhase.Postgame, MatchPhase.Warmup), made);
        Assert.Equal([new TeamMove(6, Faction.DevBlue, Faction.DevRed)], state.LastTeamMoves);
        Assert.Equal(3, state.Score.PlayerCountOf(Faction.DevBlue));
        Assert.Equal(3, state.Score.PlayerCountOf(Faction.DevRed));
    }

    [Fact]
    public void Late_joiners_during_postgame_are_included_in_the_rebalance_in_move_order()
    {
        // Blue 1 and red 2, then blue 3, 4, 5, 6 join during Postgame: blue 5, red 1. Two moves, latest joiner first.
        MatchState state = NewState();
        Assert.True(state.AddPlayer(1, Faction.DevBlue));
        Assert.True(state.AddPlayer(2, Faction.DevRed));
        for (int i = 0; i < 100 && state.Phase != MatchPhase.Postgame; i++)
            state.Advance();
        Assert.Equal(MatchPhase.Postgame, state.Phase);
        Assert.True(state.AddPlayer(3, Faction.DevBlue));
        Assert.True(state.AddPlayer(4, Faction.DevBlue));
        Assert.True(state.AddPlayer(5, Faction.DevBlue));
        Assert.True(state.AddPlayer(6, Faction.DevBlue));

        Assert.Null(state.Advance());
        Assert.Equal(new MatchTransition(MatchPhase.Postgame, MatchPhase.Warmup), state.Advance());

        Assert.Equal(
        [
            new TeamMove(6, Faction.DevBlue, Faction.DevRed),
            new TeamMove(5, Faction.DevBlue, Faction.DevRed),
        ], state.LastTeamMoves);
        Assert.Equal(
        [
            (1u, Faction.DevBlue), (2u, Faction.DevRed), (3u, Faction.DevBlue),
            (4u, Faction.DevBlue), (5u, Faction.DevRed), (6u, Faction.DevRed),
        ], Teams(state));
    }

    [Fact]
    public void Roster_that_was_rebalanced_stays_even_and_moves_nobody_at_the_next_postgame()
    {
        MatchState state = NewImbalancedState();
        AdvanceUntilBefore(state, new MatchTransition(MatchPhase.Postgame, MatchPhase.Warmup), Teams(state));
        Assert.NotNull(state.Advance());
        Assert.Single(state.LastTeamMoves);
        (uint, Faction)[] evenTeams = Teams(state);

        // A whole second match with the now-even roster ends in no moves.
        AdvanceUntilBefore(state, new MatchTransition(MatchPhase.Postgame, MatchPhase.Warmup), evenTeams);
        Assert.NotNull(state.Advance());

        Assert.Empty(state.LastTeamMoves);
        Assert.Equal(evenTeams, Teams(state));
    }
}
