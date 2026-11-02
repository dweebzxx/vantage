using Vantage.Shared.Game;
using Xunit;
namespace Vantage.Shared.Tests;

public sealed class ScoreboardOrderTests
{
    private static PlayerMatchStats Row(uint id, Faction team, int kills, int deaths) => new(id, team, kills, deaths);

    [Fact]
    public void Orders_by_kills_high_to_low()
    {
        var rows = new[]
        {
            Row(1, Faction.DevBlue, 2, 0),
            Row(2, Faction.DevBlue, 5, 9),
            Row(3, Faction.DevBlue, 3, 0),
        };

        Assert.Equal([2u, 3u, 1u], ScoreboardOrder.ForTeam(rows, Faction.DevBlue).Select(r => r.EntityId));
    }

    [Fact]
    public void Breaks_equal_kills_by_fewer_deaths()
    {
        var rows = new[]
        {
            Row(1, Faction.DevRed, 4, 3),
            Row(2, Faction.DevRed, 4, 1),
            Row(3, Faction.DevRed, 4, 2),
        };

        Assert.Equal([2u, 3u, 1u], ScoreboardOrder.ForTeam(rows, Faction.DevRed).Select(r => r.EntityId));
    }

    [Fact]
    public void Breaks_equal_kills_and_deaths_by_lower_entity_id()
    {
        var rows = new[]
        {
            Row(9, Faction.DevBlue, 1, 1),
            Row(4, Faction.DevBlue, 1, 1),
            Row(7, Faction.DevBlue, 1, 1),
        };

        Assert.Equal([4u, 7u, 9u], ScoreboardOrder.ForTeam(rows, Faction.DevBlue).Select(r => r.EntityId));
    }

    [Fact]
    public void Applies_all_three_keys_together()
    {
        // Hand-sorted: kills 5 first; then kills 3 with deaths 0 before deaths 2; then kills 3 deaths 2 by id.
        var rows = new[]
        {
            Row(6, Faction.DevBlue, 3, 2),
            Row(2, Faction.DevBlue, 3, 2),
            Row(8, Faction.DevBlue, 5, 7),
            Row(5, Faction.DevBlue, 3, 0),
            Row(1, Faction.DevBlue, 0, 0),
        };

        Assert.Equal([8u, 5u, 2u, 6u, 1u], ScoreboardOrder.ForTeam(rows, Faction.DevBlue).Select(r => r.EntityId));
    }

    [Fact]
    public void A_team_query_returns_only_that_teams_rows_and_ignores_Faction_None()
    {
        var blue = Row(1, Faction.DevBlue, 1, 0);
        var red = Row(2, Faction.DevRed, 9, 0);
        var none = Row(3, Faction.None, 99, 0);
        var rows = new[] { red, none, blue };

        Assert.Equal([blue], ScoreboardOrder.ForTeam(rows, Faction.DevBlue));
        Assert.Equal([red], ScoreboardOrder.ForTeam(rows, Faction.DevRed));
        Assert.Empty(ScoreboardOrder.ForTeam(rows, Faction.None));
        Assert.Empty(ScoreboardOrder.ForTeam(rows, (Faction)200));
    }

    [Fact]
    public void An_empty_input_gives_an_empty_result()
    {
        Assert.Empty(ScoreboardOrder.ForTeam(Array.Empty<PlayerMatchStats>(), Faction.DevBlue));
        Assert.Empty(ScoreboardOrder.ForTeam(Array.Empty<PlayerMatchStats>(), Faction.DevRed));
    }

    [Fact]
    public void A_team_with_no_rows_gives_an_empty_result()
    {
        var rows = new[] { Row(1, Faction.DevRed, 2, 2) };

        Assert.Empty(ScoreboardOrder.ForTeam(rows, Faction.DevBlue));
    }

    [Fact]
    public void The_input_is_not_modified()
    {
        var rows = new List<PlayerMatchStats>
        {
            Row(3, Faction.DevBlue, 0, 5),
            Row(1, Faction.DevRed, 7, 0),
            Row(2, Faction.DevBlue, 8, 1),
        };
        var before = rows.ToArray();

        _ = ScoreboardOrder.ForTeam(rows, Faction.DevBlue);

        Assert.Equal(before, rows);
    }

    [Fact]
    public void The_same_rows_in_any_input_order_give_the_same_output()
    {
        var rows = new[]
        {
            Row(5, Faction.DevBlue, 2, 1),
            Row(2, Faction.DevBlue, 2, 1),
            Row(7, Faction.DevBlue, 4, 4),
            Row(3, Faction.DevBlue, 2, 0),
            Row(9, Faction.DevRed, 8, 0),
            Row(1, Faction.None, 3, 3),
        };
        // Hand-sorted expectation for the Blue team.
        uint[] expected = [7, 3, 2, 5];

        foreach (var permutation in Permutations(rows))
            Assert.Equal(expected, ScoreboardOrder.ForTeam(permutation, Faction.DevBlue).Select(r => r.EntityId));
    }

    [Fact]
    public void Supports_five_players_in_a_team()
    {
        var rows = Enumerable.Range(1, 5).Select(i => Row((uint)i, Faction.DevRed, i % 2, 0)).ToArray();

        // Kills are 1,0,1,0,1 for ids 1..5: the kill-1 rows by id, then the kill-0 rows by id.
        Assert.Equal([1u, 3u, 5u, 2u, 4u], ScoreboardOrder.ForTeam(rows, Faction.DevRed).Select(r => r.EntityId));
    }

    [Fact]
    public void A_null_input_is_rejected() =>
        Assert.Throws<ArgumentNullException>(() => ScoreboardOrder.ForTeam(null!, Faction.DevBlue));

    // Every ordering of the items (Heap's algorithm), so the test covers all 720 input orders of six rows.
    private static IEnumerable<PlayerMatchStats[]> Permutations(PlayerMatchStats[] items)
    {
        var work = items.ToArray();
        var counters = new int[work.Length];
        yield return work.ToArray();
        for (int i = 0; i < work.Length;)
        {
            if (counters[i] < i)
            {
                int swap = i % 2 == 0 ? 0 : counters[i];
                (work[swap], work[i]) = (work[i], work[swap]);
                yield return work.ToArray();
                counters[i]++;
                i = 0;
            }
            else
            {
                counters[i] = 0;
                i++;
            }
        }
    }
}
