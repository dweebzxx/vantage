using Vantage.Shared.Game;
using Xunit;
namespace Vantage.Shared.Tests;

public sealed class MatchScoreSetTeamTests
{
    private const uint BlueOne = 1;
    private const uint RedTwo = 2;
    private const uint BlueThree = 3;

    // Blue has scored 2 (player 1 killed player 2 twice), red has scored 1 (player 2 killed player 3).
    // Rows: 1 blue 2k/0d, 2 red 1k/2d, 3 blue 0k/1d.
    private static MatchScore ScoredMatch()
    {
        var score = new MatchScore();
        Assert.True(score.AddPlayer(BlueOne, Faction.DevBlue));
        Assert.True(score.AddPlayer(RedTwo, Faction.DevRed));
        Assert.True(score.AddPlayer(BlueThree, Faction.DevBlue));
        Assert.True(score.RecordKill(BlueOne, Faction.DevBlue, RedTwo, Faction.DevRed));
        Assert.True(score.RecordKill(BlueOne, Faction.DevBlue, RedTwo, Faction.DevRed));
        Assert.True(score.RecordKill(RedTwo, Faction.DevRed, BlueThree, Faction.DevBlue));
        return score;
    }

    private static PlayerMatchStats[] ExpectedRows() =>
    [
        new(BlueOne, Faction.DevBlue, 2, 0),
        new(RedTwo, Faction.DevRed, 1, 2),
        new(BlueThree, Faction.DevBlue, 0, 1),
    ];

    private static void AssertUnchanged(MatchScore score)
    {
        Assert.Equal(2, score.BlueScore);
        Assert.Equal(1, score.RedScore);
        Assert.Equal(ExpectedRows(), score.Players);
        Assert.Equal(2, score.PlayerCountOf(Faction.DevBlue));
        Assert.Equal(1, score.PlayerCountOf(Faction.DevRed));
    }

    [Fact]
    public void Valid_change_returns_true_and_changes_only_the_row_team()
    {
        MatchScore score = ScoredMatch();

        Assert.True(score.SetTeam(BlueThree, Faction.DevRed));

        Assert.Equal(
        [
            new PlayerMatchStats(BlueOne, Faction.DevBlue, 2, 0),
            new PlayerMatchStats(RedTwo, Faction.DevRed, 1, 2),
            new PlayerMatchStats(BlueThree, Faction.DevRed, 0, 1),
        ], score.Players);
    }

    [Fact]
    public void Valid_change_does_not_touch_team_scores_kills_or_deaths()
    {
        MatchScore score = ScoredMatch();

        Assert.True(score.SetTeam(BlueOne, Faction.DevRed));

        Assert.Equal(2, score.BlueScore);
        Assert.Equal(1, score.RedScore);
        Assert.Equal(new PlayerMatchStats(BlueOne, Faction.DevRed, 2, 0), score.Players[0]);
        Assert.Equal(ExpectedRows()[1], score.Players[1]);
        Assert.Equal(ExpectedRows()[2], score.Players[2]);
    }

    [Fact]
    public void Valid_change_moves_the_player_between_team_counts()
    {
        MatchScore score = ScoredMatch();

        Assert.True(score.SetTeam(BlueThree, Faction.DevRed));

        Assert.Equal(1, score.PlayerCountOf(Faction.DevBlue));
        Assert.Equal(2, score.PlayerCountOf(Faction.DevRed));
    }

    [Fact]
    public void Valid_change_keeps_rows_in_entity_id_order()
    {
        MatchScore score = ScoredMatch();

        Assert.True(score.SetTeam(BlueOne, Faction.DevRed));
        Assert.True(score.SetTeam(RedTwo, Faction.DevBlue));

        Assert.Equal([BlueOne, RedTwo, BlueThree], score.Players.Select(p => p.EntityId));
    }

    [Fact]
    public void Setting_the_current_team_returns_false_and_changes_nothing()
    {
        MatchScore score = ScoredMatch();

        Assert.False(score.SetTeam(BlueOne, Faction.DevBlue));
        Assert.False(score.SetTeam(RedTwo, Faction.DevRed));

        AssertUnchanged(score);
    }

    [Fact]
    public void Absent_entity_returns_false_and_changes_nothing()
    {
        MatchScore score = ScoredMatch();

        Assert.False(score.SetTeam(99, Faction.DevRed));
        Assert.False(score.SetTeam(99, Faction.DevBlue));

        AssertUnchanged(score);
    }

    [Fact]
    public void Removed_entity_returns_false()
    {
        MatchScore score = ScoredMatch();
        Assert.True(score.RemovePlayer(RedTwo));

        Assert.False(score.SetTeam(RedTwo, Faction.DevBlue));

        Assert.Equal(2, score.Players.Count);
        Assert.Equal(2, score.PlayerCountOf(Faction.DevBlue));
    }

    [Theory]
    [InlineData(Faction.None)]
    [InlineData((Faction)99)]
    public void Non_team_faction_returns_false_and_changes_nothing(Faction team)
    {
        MatchScore score = ScoredMatch();

        Assert.False(score.SetTeam(BlueOne, team));
        Assert.False(score.SetTeam(RedTwo, team));

        AssertUnchanged(score);
    }
}
