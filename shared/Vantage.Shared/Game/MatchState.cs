namespace Vantage.Shared.Game;

/// <summary>
/// The match phase, its timer, and the score. The server calls <see cref="Advance"/> once per tick.
/// </summary>
public sealed class MatchState
{
    private readonly MatchScore _score = new();

    public MatchState(MatchTiming? timing = null) => Timing = timing ?? MatchTiming.Default;

    public MatchTiming Timing { get; }
    public MatchPhase Phase { get; private set; } = MatchPhase.Idle;
    public uint TicksRemaining { get; private set; }
    public MatchScore Score => _score;
    public int PlayerCount => _score.Players.Count;

    /// <summary>
    /// The team moves made by the latest <see cref="Advance"/> call: the rebalance on the postgame-to-warmup
    /// transition, otherwise empty.
    /// </summary>
    public IReadOnlyList<TeamMove> LastTeamMoves { get; private set; } = Array.Empty<TeamMove>();

    public bool AddPlayer(uint entityId, Faction team) => _score.AddPlayer(entityId, team);

    public bool RemovePlayer(uint entityId) => _score.RemovePlayer(entityId);

    public Faction ChooseTeamForNewPlayer() =>
        TeamAssignment.Choose(_score.PlayerCountOf(Faction.DevBlue), _score.PlayerCountOf(Faction.DevRed));

    /// <summary>Scores a kill only while <see cref="MatchPhase.Playing"/>.</summary>
    public bool RecordKill(uint killerEntityId, Faction killerFaction, uint victimEntityId, Faction victimFaction) =>
        Phase == MatchPhase.Playing &&
        _score.RecordKill(killerEntityId, killerFaction, victimEntityId, victimFaction);

    /// <summary>Advances one tick. Returns the transition made, or null when the phase did not change.</summary>
    public MatchTransition? Advance()
    {
        LastTeamMoves = Array.Empty<TeamMove>();

        if (PlayerCount == 0)
        {
            if (Phase == MatchPhase.Idle)
                return null;

            TicksRemaining = 0;
            _score.Clear();
            return Enter(MatchPhase.Idle);
        }

        if (Phase == MatchPhase.Idle)
        {
            TicksRemaining = Timing.WarmupTicks;
            _score.Reset();
            return Enter(MatchPhase.Warmup);
        }

        TicksRemaining--;
        if (TicksRemaining != 0)
            return null;

        switch (Phase)
        {
            case MatchPhase.Warmup:
                TicksRemaining = Timing.PlayingTicks;
                _score.Reset();
                return Enter(MatchPhase.Playing);
            case MatchPhase.Playing:
                TicksRemaining = Timing.PostgameTicks;
                return Enter(MatchPhase.Postgame);
            default:
                TicksRemaining = Timing.WarmupTicks;
                _score.Reset();
                RebalanceTeams();
                return Enter(MatchPhase.Warmup);
        }
    }

    // Runs only when a new match starts after postgame. The score is already reset, so a move changes only the team.
    private void RebalanceTeams()
    {
        IReadOnlyList<TeamMove> moves = TeamAssignment.Rebalance(_score.Players.Select(p => (p.EntityId, p.Team)));
        foreach (TeamMove move in moves)
            _score.SetTeam(move.EntityId, move.To);
        LastTeamMoves = moves;
    }

    private MatchTransition Enter(MatchPhase next)
    {
        var transition = new MatchTransition(Phase, next);
        Phase = next;
        return transition;
    }
}
