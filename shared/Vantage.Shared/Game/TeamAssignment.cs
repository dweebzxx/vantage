namespace Vantage.Shared.Game;

public static class TeamAssignment
{
    /// <summary>Picks the team with fewer players; equal counts give <see cref="Faction.DevBlue"/>.</summary>
    public static Faction Choose(int blueCount, int redCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(blueCount);
        ArgumentOutOfRangeException.ThrowIfNegative(redCount);
        return redCount < blueCount ? Faction.DevRed : Faction.DevBlue;
    }

    /// <summary>
    /// Evens out the teams. While the team sizes differ by more than 1, moves the player with the highest
    /// entity id (the latest joiner) from the larger team to the smaller one. Returns the moves in the order
    /// made. Entries whose team is not <see cref="Faction.DevBlue"/> or <see cref="Faction.DevRed"/> are
    /// ignored. The input is not changed, and the result does not depend on the input order.
    /// </summary>
    public static IReadOnlyList<TeamMove> Rebalance(IEnumerable<(uint EntityId, Faction Team)> players)
    {
        ArgumentNullException.ThrowIfNull(players);

        // Ascending by id, so the last element of a list is the latest joiner.
        var blue = new List<uint>();
        var red = new List<uint>();
        foreach ((uint entityId, Faction team) in players)
        {
            if (team == Faction.DevBlue)
                blue.Add(entityId);
            else if (team == Faction.DevRed)
                red.Add(entityId);
        }

        blue.Sort();
        red.Sort();

        var moves = new List<TeamMove>();
        while (blue.Count - red.Count > 1)
        {
            uint moved = blue[^1];
            blue.RemoveAt(blue.Count - 1);
            red.Add(moved);
            moves.Add(new TeamMove(moved, Faction.DevBlue, Faction.DevRed));
        }

        while (red.Count - blue.Count > 1)
        {
            uint moved = red[^1];
            red.RemoveAt(red.Count - 1);
            blue.Add(moved);
            moves.Add(new TeamMove(moved, Faction.DevRed, Faction.DevBlue));
        }

        return moves;
    }
}
