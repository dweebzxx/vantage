using Vantage.Shared.Game;
using Xunit;
namespace Vantage.Shared.Tests;

public sealed class MatchScoreTests
{
    private const uint BlueOne = 1;
    private const uint RedTwo = 2;
    private const uint BlueThree = 3;

    private static MatchScore ScoreWithThreePlayers()
    {
        var score = new MatchScore();
        Assert.True(score.AddPlayer(BlueOne, Faction.DevBlue));
        Assert.True(score.AddPlayer(RedTwo, Faction.DevRed));
        Assert.True(score.AddPlayer(BlueThree, Faction.DevBlue));
        return score;
    }

    private static void AssertNothingChanged(MatchScore score)
    {
        Assert.Equal(0, score.ScoreOf(Faction.DevBlue));
        Assert.Equal(0, score.ScoreOf(Faction.DevRed));
        Assert.All(score.Players, row =>
        {
            Assert.Equal(0, row.Kills);
            Assert.Equal(0, row.Deaths);
        });
    }

    [Fact]
    public void New_score_is_empty_and_zero()
    {
        var score = new MatchScore();

        Assert.Empty(score.Players);
        Assert.Equal(0, score.ScoreOf(Faction.DevBlue));
        Assert.Equal(0, score.ScoreOf(Faction.DevRed));
        Assert.Equal(0, score.ScoreOf(Faction.None));
    }

    [Fact]
    public void Rows_are_ordered_by_entity_id_regardless_of_join_order()
    {
        var score = new MatchScore();
        score.AddPlayer(5, Faction.DevRed);
        score.AddPlayer(1, Faction.DevBlue);
        score.AddPlayer(3, Faction.DevBlue);
        score.AddPlayer(4, Faction.DevRed);

        Assert.Equal(new uint[] { 1, 3, 4, 5 }, score.Players.Select(p => p.EntityId));
        Assert.Equal(Faction.DevRed, score.Players[2].Team);
    }

    [Fact]
    public void Duplicate_player_is_rejected_and_changes_nothing()
    {
        MatchScore score = ScoreWithThreePlayers();

        Assert.False(score.AddPlayer(BlueOne, Faction.DevRed));

        Assert.Equal(3, score.Players.Count);
        Assert.Equal(Faction.DevBlue, score.Players[0].Team);
    }

    [Theory]
    [InlineData(Faction.None)]
    [InlineData((Faction)99)]
    [InlineData((Faction)(-1))]
    public void Non_team_faction_is_rejected_and_adds_no_row(Faction team)
    {
        var score = new MatchScore();

        Assert.False(score.AddPlayer(7, team));

        Assert.Empty(score.Players);
    }

    [Fact]
    public void Removing_a_player_drops_the_row_and_keeps_team_scores()
    {
        MatchScore score = ScoreWithThreePlayers();
        score.RecordKill(BlueOne, Faction.DevBlue, RedTwo, Faction.DevRed);

        Assert.True(score.RemovePlayer(BlueOne));

        Assert.Equal(new uint[] { RedTwo, BlueThree }, score.Players.Select(p => p.EntityId));
        Assert.Equal(1, score.ScoreOf(Faction.DevBlue));
        Assert.Equal(0, score.ScoreOf(Faction.DevRed));
    }

    [Fact]
    public void Removing_an_absent_player_returns_false_and_changes_nothing()
    {
        MatchScore score = ScoreWithThreePlayers();

        Assert.False(score.RemovePlayer(99));

        Assert.Equal(3, score.Players.Count);
    }

    [Fact]
    public void Valid_kill_scores_for_the_killers_team_and_updates_both_rows()
    {
        MatchScore score = ScoreWithThreePlayers();

        Assert.True(score.RecordKill(BlueOne, Faction.DevBlue, RedTwo, Faction.DevRed));

        Assert.Equal(1, score.ScoreOf(Faction.DevBlue));
        Assert.Equal(0, score.ScoreOf(Faction.DevRed));
        Assert.Equal(new PlayerMatchStats(BlueOne, Faction.DevBlue, 1, 0), score.Players[0]);
        Assert.Equal(new PlayerMatchStats(RedTwo, Faction.DevRed, 0, 1), score.Players[1]);
        Assert.Equal(new PlayerMatchStats(BlueThree, Faction.DevBlue, 0, 0), score.Players[2]);
    }

    [Fact]
    public void Red_kill_scores_for_red()
    {
        MatchScore score = ScoreWithThreePlayers();

        Assert.True(score.RecordKill(RedTwo, Faction.DevRed, BlueThree, Faction.DevBlue));

        Assert.Equal(0, score.ScoreOf(Faction.DevBlue));
        Assert.Equal(1, score.ScoreOf(Faction.DevRed));
    }

    [Theory]
    [InlineData(BlueOne, Faction.DevBlue, BlueThree, Faction.DevBlue)] // same team
    [InlineData(RedTwo, Faction.DevRed, RedTwo, Faction.DevRed)] // self, same team
    [InlineData(BlueOne, Faction.DevBlue, BlueOne, Faction.DevRed)] // self, different factions
    [InlineData(BlueOne, Faction.None, RedTwo, Faction.DevRed)]
    [InlineData(BlueOne, Faction.DevBlue, RedTwo, Faction.None)]
    [InlineData(BlueOne, Faction.None, RedTwo, Faction.None)]
    [InlineData(BlueOne, (Faction)99, RedTwo, Faction.DevRed)]
    [InlineData(BlueOne, Faction.DevBlue, RedTwo, (Faction)99)]
    [InlineData(BlueOne, (Faction)(-1), RedTwo, Faction.DevRed)]
    [InlineData(BlueOne, Faction.DevBlue, RedTwo, (Faction)(-1))]
    public void Invalid_kill_is_rejected_and_changes_nothing(uint killer, Faction killerFaction, uint victim, Faction victimFaction)
    {
        MatchScore score = ScoreWithThreePlayers();

        Assert.False(score.RecordKill(killer, killerFaction, victim, victimFaction));

        AssertNothingChanged(score);
    }

    [Fact]
    public void Kill_by_a_player_who_left_still_scores_the_team_point_and_adds_no_row()
    {
        MatchScore score = ScoreWithThreePlayers();
        score.RemovePlayer(BlueOne);

        Assert.True(score.RecordKill(BlueOne, Faction.DevBlue, RedTwo, Faction.DevRed));

        Assert.Equal(1, score.ScoreOf(Faction.DevBlue));
        Assert.Equal(new uint[] { RedTwo, BlueThree }, score.Players.Select(p => p.EntityId));
        Assert.Equal(new PlayerMatchStats(RedTwo, Faction.DevRed, 0, 1), score.Players[0]);
        Assert.Equal(new PlayerMatchStats(BlueThree, Faction.DevBlue, 0, 0), score.Players[1]);
    }

    [Fact]
    public void Kill_of_a_player_who_left_scores_and_credits_the_killer_only()
    {
        MatchScore score = ScoreWithThreePlayers();
        score.RemovePlayer(RedTwo);

        Assert.True(score.RecordKill(BlueOne, Faction.DevBlue, RedTwo, Faction.DevRed));

        Assert.Equal(1, score.ScoreOf(Faction.DevBlue));
        Assert.Equal(new PlayerMatchStats(BlueOne, Faction.DevBlue, 1, 0), score.Players[0]);
        Assert.Equal(2, score.Players.Count);
    }

    [Fact]
    public void Ten_valid_kills_leave_team_scores_and_row_totals_in_agreement()
    {
        // Blue 1 and 3 against red 2 and 4, all present throughout.
        var score = new MatchScore();
        score.AddPlayer(1, Faction.DevBlue);
        score.AddPlayer(2, Faction.DevRed);
        score.AddPlayer(3, Faction.DevBlue);
        score.AddPlayer(4, Faction.DevRed);

        (uint Killer, Faction KillerTeam, uint Victim, Faction VictimTeam)[] kills =
        {
            (1, Faction.DevBlue, 2, Faction.DevRed),
            (1, Faction.DevBlue, 4, Faction.DevRed),
            (1, Faction.DevBlue, 2, Faction.DevRed),
            (3, Faction.DevBlue, 4, Faction.DevRed),
            (3, Faction.DevBlue, 2, Faction.DevRed),
            (2, Faction.DevRed, 1, Faction.DevBlue),
            (2, Faction.DevRed, 3, Faction.DevBlue),
            (4, Faction.DevRed, 1, Faction.DevBlue),
            (4, Faction.DevRed, 3, Faction.DevBlue),
            (3, Faction.DevBlue, 4, Faction.DevRed),
        };

        foreach (var k in kills)
            Assert.True(score.RecordKill(k.Killer, k.KillerTeam, k.Victim, k.VictimTeam));

        Assert.Equal(6, score.ScoreOf(Faction.DevBlue));
        Assert.Equal(4, score.ScoreOf(Faction.DevRed));
        Assert.Equal(10, score.ScoreOf(Faction.DevBlue) + score.ScoreOf(Faction.DevRed));
        Assert.Equal(10, score.Players.Sum(p => p.Kills));
        Assert.Equal(10, score.Players.Sum(p => p.Deaths));
        // Hand-counted from the kill list above.
        Assert.Equal(new PlayerMatchStats(1, Faction.DevBlue, 3, 2), score.Players[0]);
        Assert.Equal(new PlayerMatchStats(2, Faction.DevRed, 2, 3), score.Players[1]);
        Assert.Equal(new PlayerMatchStats(3, Faction.DevBlue, 3, 2), score.Players[2]);
        Assert.Equal(new PlayerMatchStats(4, Faction.DevRed, 2, 3), score.Players[3]);
    }

    [Fact]
    public void Outcome_is_blue_wins_red_wins_or_draw()
    {
        var score = ScoreWithThreePlayers();
        Assert.Equal(MatchOutcome.Draw, score.GetOutcome());

        score.RecordKill(BlueOne, Faction.DevBlue, RedTwo, Faction.DevRed);
        Assert.Equal(MatchOutcome.BlueWins, score.GetOutcome());

        score.RecordKill(RedTwo, Faction.DevRed, BlueOne, Faction.DevBlue);
        Assert.Equal(MatchOutcome.Draw, score.GetOutcome());

        score.RecordKill(RedTwo, Faction.DevRed, BlueThree, Faction.DevBlue);
        Assert.Equal(MatchOutcome.RedWins, score.GetOutcome());
    }

    [Fact]
    public void Reset_zeroes_scores_and_row_stats_but_keeps_rows()
    {
        MatchScore score = ScoreWithThreePlayers();
        score.RecordKill(BlueOne, Faction.DevBlue, RedTwo, Faction.DevRed);
        score.RecordKill(RedTwo, Faction.DevRed, BlueThree, Faction.DevBlue);

        score.Reset();

        AssertNothingChanged(score);
        Assert.Equal(new uint[] { BlueOne, RedTwo, BlueThree }, score.Players.Select(p => p.EntityId));
        Assert.Equal(new[] { Faction.DevBlue, Faction.DevRed, Faction.DevBlue }, score.Players.Select(p => p.Team));
    }

    [Fact]
    public void Clear_removes_rows_and_scores()
    {
        MatchScore score = ScoreWithThreePlayers();
        score.RecordKill(BlueOne, Faction.DevBlue, RedTwo, Faction.DevRed);

        score.Clear();

        Assert.Empty(score.Players);
        Assert.Equal(0, score.ScoreOf(Faction.DevBlue));
        Assert.Equal(0, score.ScoreOf(Faction.DevRed));
    }

    [Fact]
    public void Score_of_a_non_team_faction_is_zero_even_after_kills()
    {
        MatchScore score = ScoreWithThreePlayers();
        score.RecordKill(BlueOne, Faction.DevBlue, RedTwo, Faction.DevRed);

        Assert.Equal(0, score.ScoreOf(Faction.None));
        Assert.Equal(0, score.ScoreOf((Faction)99));
    }

    [Fact]
    public void Player_count_of_counts_rows_per_team()
    {
        MatchScore score = ScoreWithThreePlayers();

        Assert.Equal(2, score.PlayerCountOf(Faction.DevBlue));
        Assert.Equal(1, score.PlayerCountOf(Faction.DevRed));
        Assert.Equal(0, score.PlayerCountOf(Faction.None));
    }
}
