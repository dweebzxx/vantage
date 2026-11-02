namespace Vantage.Shared.Game;

/// <summary>Pure ordering rule for the rows of one team on the scoreboard.</summary>
public static class ScoreboardOrder
{
    /// <summary>
    /// The rows of <paramref name="team"/> in display order: kills high to low, then deaths low to high,
    /// then entity id low to high. Rows of any other team, and every row when <paramref name="team"/> is not
    /// <see cref="Faction.DevBlue"/> or <see cref="Faction.DevRed"/>, are never returned. The input is not changed,
    /// and the result does not depend on the input order.
    /// </summary>
    public static IReadOnlyList<PlayerMatchStats> ForTeam(IEnumerable<PlayerMatchStats> rows, Faction team)
    {
        ArgumentNullException.ThrowIfNull(rows);
        if (!MatchRules.IsTeam(team))
            return Array.Empty<PlayerMatchStats>();

        return rows
            .Where(row => row.Team == team)
            .OrderByDescending(row => row.Kills)
            .ThenBy(row => row.Deaths)
            .ThenBy(row => row.EntityId)
            .ToArray();
    }
}
