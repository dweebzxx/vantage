namespace Vantage.Shared.Game;

/// <summary>Team scores plus per-player kills and deaths for one match.</summary>
public sealed class MatchScore
{
    // Rows stay sorted by EntityId so the scoreboard order is deterministic.
    private readonly List<PlayerMatchStats> _rows = new();

    public MatchScore() => Players = _rows.AsReadOnly();

    public int BlueScore { get; private set; }
    public int RedScore { get; private set; }

    /// <summary>Rows in ascending EntityId order.</summary>
    public IReadOnlyList<PlayerMatchStats> Players { get; }

    public int ScoreOf(Faction team) => team switch
    {
        Faction.DevBlue => BlueScore,
        Faction.DevRed => RedScore,
        _ => 0,
    };

    public int PlayerCountOf(Faction team)
    {
        int count = 0;
        foreach (PlayerMatchStats row in _rows)
        {
            if (row.Team == team)
                count++;
        }

        return count;
    }

    public bool AddPlayer(uint entityId, Faction team)
    {
        if (!MatchRules.IsTeam(team))
            return false;

        int index = Find(entityId);
        if (index >= 0)
            return false;

        _rows.Insert(~index, new PlayerMatchStats(entityId, team, 0, 0));
        return true;
    }

    /// <summary>
    /// Changes the row's team. Returns true only when the row exists, <paramref name="team"/> is a team, and it
    /// differs from the row's current team. Scores, kills, and deaths are never changed.
    /// </summary>
    public bool SetTeam(uint entityId, Faction team)
    {
        if (!MatchRules.IsTeam(team))
            return false;

        int index = Find(entityId);
        if (index < 0 || _rows[index].Team == team)
            return false;

        _rows[index] = _rows[index] with { Team = team };
        return true;
    }

    /// <summary>Removes the player's row. Team scores are unchanged.</summary>
    public bool RemovePlayer(uint entityId)
    {
        int index = Find(entityId);
        if (index < 0)
            return false;

        _rows.RemoveAt(index);
        return true;
    }

    /// <summary>
    /// Scores one kill for the killer's team when both factions are teams, differ, and the entities differ.
    /// Row stats update only for rows that still exist.
    /// </summary>
    public bool RecordKill(uint killerEntityId, Faction killerFaction, uint victimEntityId, Faction victimFaction)
    {
        if (!MatchRules.IsTeam(killerFaction) || !MatchRules.IsTeam(victimFaction) ||
            killerFaction == victimFaction || killerEntityId == victimEntityId)
        {
            return false;
        }

        if (killerFaction == Faction.DevBlue)
            BlueScore++;
        else
            RedScore++;

        int killerIndex = Find(killerEntityId);
        if (killerIndex >= 0)
            _rows[killerIndex] = _rows[killerIndex] with { Kills = _rows[killerIndex].Kills + 1 };

        int victimIndex = Find(victimEntityId);
        if (victimIndex >= 0)
            _rows[victimIndex] = _rows[victimIndex] with { Deaths = _rows[victimIndex].Deaths + 1 };

        return true;
    }

    public MatchOutcome GetOutcome() => MatchRules.OutcomeOf(BlueScore, RedScore);

    /// <summary>Zeroes team scores and every row's kills and deaths; keeps the rows.</summary>
    public void Reset()
    {
        BlueScore = 0;
        RedScore = 0;
        for (int i = 0; i < _rows.Count; i++)
            _rows[i] = _rows[i] with { Kills = 0, Deaths = 0 };
    }

    /// <summary>Removes every row and zeroes team scores.</summary>
    public void Clear()
    {
        BlueScore = 0;
        RedScore = 0;
        _rows.Clear();
    }

    // Binary search by EntityId: index when found, otherwise the bitwise complement of the insert position.
    private int Find(uint entityId)
    {
        int low = 0;
        int high = _rows.Count - 1;
        while (low <= high)
        {
            int mid = low + ((high - low) >> 1);
            uint midId = _rows[mid].EntityId;
            if (midId == entityId)
                return mid;
            if (midId < entityId)
                low = mid + 1;
            else
                high = mid - 1;
        }

        return ~low;
    }
}
