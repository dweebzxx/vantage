using Vantage.Shared.Game;
using Xunit;
namespace Vantage.Shared.Tests;

public sealed class MatchOutcomeRuleTests
{
    // Every expected outcome is written by hand from "higher score wins, equal scores draw".
    public static TheoryData<int, int, MatchOutcome> Cases() => new()
    {
        { 1, 0, MatchOutcome.BlueWins },
        { 0, 1, MatchOutcome.RedWins },
        { 5, 3, MatchOutcome.BlueWins },
        { 3, 5, MatchOutcome.RedWins },
        { 0, 0, MatchOutcome.Draw },
        { 4, 4, MatchOutcome.Draw },
        { 1_000_000, 1_000_000, MatchOutcome.Draw },
        { int.MaxValue, int.MaxValue, MatchOutcome.Draw },
        { 8, 7, MatchOutcome.BlueWins },
        { 7, 8, MatchOutcome.RedWins },
        { int.MaxValue, int.MaxValue - 1, MatchOutcome.BlueWins },
        { 0, int.MaxValue, MatchOutcome.RedWins },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void OutcomeOf_follows_the_higher_score(int blue, int red, MatchOutcome expected) =>
        Assert.Equal(expected, MatchRules.OutcomeOf(blue, red));

    [Fact]
    public void OutcomeOf_treats_a_negative_score_as_an_ordinary_integer()
    {
        Assert.Equal(MatchOutcome.BlueWins, MatchRules.OutcomeOf(0, -1));
        Assert.Equal(MatchOutcome.RedWins, MatchRules.OutcomeOf(-3, -2));
        Assert.Equal(MatchOutcome.Draw, MatchRules.OutcomeOf(-2, -2));
    }

    [Fact]
    public void GetOutcome_agrees_with_OutcomeOf_for_scores_built_through_RecordKill()
    {
        var score = new MatchScore();
        Assert.True(score.AddPlayer(1, Faction.DevBlue));
        Assert.True(score.AddPlayer(2, Faction.DevRed));

        // After each kill the hand-counted scores are 0:0, 1:0, 1:1, 1:2, 2:2, 3:2.
        (uint Killer, Faction KillerTeam, uint Victim, Faction VictimTeam, int Blue, int Red, MatchOutcome Expected)[] steps =
        [
            (1, Faction.DevBlue, 2, Faction.DevRed, 1, 0, MatchOutcome.BlueWins),
            (2, Faction.DevRed, 1, Faction.DevBlue, 1, 1, MatchOutcome.Draw),
            (2, Faction.DevRed, 1, Faction.DevBlue, 1, 2, MatchOutcome.RedWins),
            (1, Faction.DevBlue, 2, Faction.DevRed, 2, 2, MatchOutcome.Draw),
            (1, Faction.DevBlue, 2, Faction.DevRed, 3, 2, MatchOutcome.BlueWins),
        ];

        Assert.Equal(MatchRules.OutcomeOf(0, 0), score.GetOutcome());
        foreach (var step in steps)
        {
            Assert.True(score.RecordKill(step.Killer, step.KillerTeam, step.Victim, step.VictimTeam));
            Assert.Equal((step.Blue, step.Red), (score.BlueScore, score.RedScore));
            Assert.Equal(step.Expected, score.GetOutcome());
            Assert.Equal(MatchRules.OutcomeOf(score.BlueScore, score.RedScore), score.GetOutcome());
        }
    }
}
