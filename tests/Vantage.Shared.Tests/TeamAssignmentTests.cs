using Vantage.Shared.Game;
using Xunit;
namespace Vantage.Shared.Tests;

public sealed class TeamAssignmentTests
{
    [Theory]
    [InlineData(0, 0, Faction.DevBlue)]
    [InlineData(1, 1, Faction.DevBlue)]
    [InlineData(4, 4, Faction.DevBlue)]
    [InlineData(1, 0, Faction.DevRed)]
    [InlineData(5, 2, Faction.DevRed)]
    [InlineData(0, 1, Faction.DevBlue)]
    [InlineData(2, 5, Faction.DevBlue)]
    public void Joins_the_team_with_fewer_players_and_blue_when_equal(int blue, int red, Faction expected) =>
        Assert.Equal(expected, TeamAssignment.Choose(blue, red));

    [Theory]
    [InlineData(-1, 0, "blueCount")]
    [InlineData(0, -1, "redCount")]
    [InlineData(-3, -3, "blueCount")]
    public void Negative_count_is_rejected(int blue, int red, string parameter)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => TeamAssignment.Choose(blue, red));
        Assert.Equal(parameter, ex.ParamName);
    }
}
