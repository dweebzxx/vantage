using Vantage.Shared.Game;
using Xunit;
namespace Vantage.Shared.Tests;

public sealed class TeamRebalanceTests
{
    // Heavy-team ids are 10, 20, 30 ... and light-team ids are 15, 25, 35 ..., so the two teams interleave.
    // Expected ids are the highest heavy-team ids, in the order they must move.
    public static TheoryData<int, int, uint[]> SizeTable => new()
    {
        { 0, 0, [] },
        { 1, 0, [] },
        { 2, 0, [10 * 2] },
        { 3, 0, [10 * 3] },
        { 4, 0, [10 * 4, 10 * 3] },
        { 1, 1, [] },
        { 2, 1, [] },
        { 3, 1, [10 * 3] },
        { 4, 1, [10 * 4] },
        { 2, 2, [] },
        { 3, 2, [] },
        { 5, 1, [10 * 5, 10 * 4] },
    };

    private static List<(uint EntityId, Faction Team)> Roster(Faction heavy, int heavyCount, int lightCount)
    {
        Faction light = heavy == Faction.DevBlue ? Faction.DevRed : Faction.DevBlue;
        var players = new List<(uint, Faction)>();
        for (int i = 1; i <= heavyCount; i++)
            players.Add(((uint)(10 * i), heavy));
        for (int i = 1; i <= lightCount; i++)
            players.Add(((uint)(10 * i + 5), light));
        return players;
    }

    private static int SizeAfter(IEnumerable<(uint EntityId, Faction Team)> players, IReadOnlyList<TeamMove> moves, Faction team)
    {
        var teamOf = players.ToDictionary(p => p.EntityId, p => p.Team);
        foreach (TeamMove move in moves)
            teamOf[move.EntityId] = move.To;
        return teamOf.Values.Count(t => t == team);
    }

    [Theory]
    [MemberData(nameof(SizeTable))]
    public void Blue_heavy_roster_moves_the_latest_joiners_to_red(int blue, int red, uint[] expectedIds)
    {
        List<(uint EntityId, Faction Team)> players = Roster(Faction.DevBlue, blue, red);

        IReadOnlyList<TeamMove> moves = TeamAssignment.Rebalance(players);

        Assert.Equal(expectedIds.Select(id => new TeamMove(id, Faction.DevBlue, Faction.DevRed)), moves);
        Assert.InRange(SizeAfter(players, moves, Faction.DevBlue) - SizeAfter(players, moves, Faction.DevRed), -1, 1);
    }

    [Theory]
    [MemberData(nameof(SizeTable))]
    public void Red_heavy_roster_moves_the_latest_joiners_to_blue(int red, int blue, uint[] expectedIds)
    {
        List<(uint EntityId, Faction Team)> players = Roster(Faction.DevRed, red, blue);

        IReadOnlyList<TeamMove> moves = TeamAssignment.Rebalance(players);

        Assert.Equal(expectedIds.Select(id => new TeamMove(id, Faction.DevRed, Faction.DevBlue)), moves);
        Assert.InRange(SizeAfter(players, moves, Faction.DevBlue) - SizeAfter(players, moves, Faction.DevRed), -1, 1);
    }

    [Fact]
    public void Empty_input_gives_no_moves() =>
        Assert.Empty(TeamAssignment.Rebalance(Array.Empty<(uint, Faction)>()));

    [Fact]
    public void Null_input_is_rejected()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => TeamAssignment.Rebalance(null!));
        Assert.Equal("players", ex.ParamName);
    }

    [Fact]
    public void Moved_player_is_the_highest_id_on_the_larger_team_not_the_highest_overall()
    {
        // Blue has 4 players, red has 1 whose id (50) is the highest overall.
        (uint, Faction)[] players =
        [
            (7, Faction.DevBlue), (3, Faction.DevBlue), (9, Faction.DevBlue), (1, Faction.DevBlue),
            (50, Faction.DevRed),
        ];

        IReadOnlyList<TeamMove> moves = TeamAssignment.Rebalance(players);

        Assert.Equal([new TeamMove(9, Faction.DevBlue, Faction.DevRed)], moves);
    }

    [Fact]
    public void Input_order_does_not_change_the_moves()
    {
        (uint, Faction)[] ascending = Enumerable.Range(1, 6).Select(i => ((uint)(2 * i), Faction.DevRed)).ToArray();
        TeamMove[] expected =
        [
            new(12, Faction.DevRed, Faction.DevBlue),
            new(10, Faction.DevRed, Faction.DevBlue),
            new(8, Faction.DevRed, Faction.DevBlue),
        ];

        Assert.Equal(expected, TeamAssignment.Rebalance(ascending));
        Assert.Equal(expected, TeamAssignment.Rebalance(ascending.Reverse()));
        Assert.Equal(expected, TeamAssignment.Rebalance(new (uint, Faction)[]
        {
            ascending[3], ascending[0], ascending[5], ascending[2], ascending[4], ascending[1],
        }));
    }

    [Fact]
    public void Input_order_does_not_change_the_moves_when_both_teams_have_players()
    {
        (uint, Faction)[] ordered =
        [
            (1, Faction.DevBlue), (2, Faction.DevRed), (3, Faction.DevBlue), (4, Faction.DevBlue), (5, Faction.DevBlue),
        ];
        TeamMove[] expected = [new(5, Faction.DevBlue, Faction.DevRed)];

        Assert.Equal(expected, TeamAssignment.Rebalance(ordered));
        Assert.Equal(expected, TeamAssignment.Rebalance(ordered.Reverse()));
        Assert.Equal(expected, TeamAssignment.Rebalance(new (uint, Faction)[]
        {
            ordered[2], ordered[4], ordered[1], ordered[0], ordered[3],
        }));
    }

    [Fact]
    public void Entries_that_are_not_on_a_team_are_ignored()
    {
        // Three blue and no red. The None and undefined entries have the highest ids and must neither move nor count.
        (uint, Faction)[] players =
        [
            (1, Faction.DevBlue), (2, Faction.DevBlue), (3, Faction.DevBlue),
            (100, Faction.None), (101, (Faction)99), (102, Faction.None),
        ];

        IReadOnlyList<TeamMove> moves = TeamAssignment.Rebalance(players);

        Assert.Equal([new TeamMove(3, Faction.DevBlue, Faction.DevRed)], moves);
    }

    [Fact]
    public void Roster_of_only_non_team_entries_gives_no_moves()
    {
        (uint, Faction)[] players = [(1, Faction.None), (2, (Faction)7), (3, Faction.None)];

        Assert.Empty(TeamAssignment.Rebalance(players));
    }

    [Fact]
    public void Input_collection_is_not_modified()
    {
        var players = new List<(uint EntityId, Faction Team)>
        {
            (8, Faction.DevBlue), (2, Faction.DevBlue), (6, Faction.DevBlue), (4, Faction.DevBlue), (5, Faction.None),
        };
        (uint, Faction)[] before = players.ToArray();

        IReadOnlyList<TeamMove> moves = TeamAssignment.Rebalance(players);

        Assert.Equal(2, moves.Count);
        Assert.Equal(before, players);
    }
}
